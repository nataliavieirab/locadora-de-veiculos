using System.Security.Cryptography;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Fotos;

public sealed record PendenciaFoto(Guid FotoId, Guid TokenPosse, ObjetoFoto Objeto);

public sealed class CasosFotos(IRepositorioVeiculos veiculos, IRepositorioFotos fotos,
    IVinculosAluguelVeiculo alugueis, IUnidadeTrabalhoEmpresa unidade, ISessaoFotos sessao,
    IProcessadorImagem processador, IArmazenamentoObjeto objetos, DestinoFotos destino,
    IAuditoriaCadastro auditoria, TimeProvider tempo, IRetomadaFotos? retomada = null)
{
    // Deve ser chamado antes de consumir/processar bytes do multipart.
    public async Task<Veiculo> AutorizarAsync(Guid veiculoId, string? versao, CancellationToken cancelamento)
    {
        if (retomada is not null) await retomada.RetomarAsync(cancelamento);
        return await unidade.ExecutarAsync(token => ExigirVeiculoAsync(veiculoId, versao, token), cancelamento);
    }

    public async Task<Veiculo> EnviarAsync(Guid veiculoId, string? versao, Stream arquivo, string mime,
        CancellationToken cancelamento)
    {
        await AutorizarAsync(veiculoId, versao, cancelamento);
        var imagem = await processador.ProcessarAsync(arquivo, mime, cancelamento);
        var pendencia = await PrepararAsync(veiculoId, versao, imagem, cancelamento);
        // Não compensar no catch: o PUT ou o commit podem ter resultado incerto.
        // O registro durável da pendência precede qualquer I/O.
        await objetos.GravarAsync(pendencia.Objeto, imagem.Bytes, cancelamento);
        await unidade.ExecutarAsync(async token =>
        {
            var trabalho = await fotos.TrabalhoAsync(pendencia.FotoId, token) ?? throw new CadastroNaoEncontradoException();
            trabalho.ConfirmarEnvio(pendencia.TokenPosse, tempo.GetUtcNow());
            return true;
        }, cancelamento);
        pendencia.Objeto.Conferir(await objetos.LerAsync(pendencia.Objeto, cancelamento));
        return await AtivarAsync(veiculoId, versao, pendencia, cancelamento);
    }

    public Task<PendenciaFoto> PrepararAsync(Guid veiculoId, string? versao, ImagemSaneada imagem,
        CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var veiculo = await ExigirVeiculoAsync(veiculoId, versao, token);
            var agora = tempo.GetUtcNow();
            var foto = FotoObjeto.Preparar(veiculo, imagem.Mime, imagem.Bytes.Length, imagem.Largura,
                imagem.Altura, SHA256.HashData(imagem.Bytes), veiculo.RowVersion, destino.Provedor,
                destino.Bucket, sessao.AtorId, sessao.Correlacao, agora);
            var trabalho = TrabalhoObjeto.Criar(foto, agora);
            var posse = trabalho.Adquirir(agora);
            fotos.Adicionar(foto, trabalho);
            return new PendenciaFoto(foto.Id, posse, Objeto(foto));
        }, cancelamento);

    private Task<Veiculo> AtivarAsync(Guid veiculoId, string? versao, PendenciaFoto pendencia,
        CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var veiculo = await ExigirVeiculoAsync(veiculoId, versao, token);
            var foto = await fotos.ConsultarAsync(pendencia.FotoId, token)
                ?? throw new CadastroNaoEncontradoException();
            var trabalho = await fotos.TrabalhoAsync(foto.Id, token) ?? throw new CadastroNaoEncontradoException();
            var agora = tempo.GetUtcNow();
            trabalho.ExigirPosse(pendencia.TokenPosse, agora);
            if (foto.VeiculoId != veiculo.Id || foto.Estado != EstadoFoto.Pendente
                || !foto.VersaoBase.SequenceEqual(veiculo.RowVersion))
                throw new PrecondicaoCadastroException(ausente: false);
            var anterior = veiculo.FotoAtivaId is Guid id ? await fotos.ConsultarAsync(id, token) : null;
            if (anterior is not null)
            {
                veiculo.RemoverFoto(anterior, agora);
                var retencao = await fotos.TrabalhoAsync(anterior.Id, token) ?? throw new CadastroNaoEncontradoException();
                retencao.AgendarRetencao(anterior, agora);
                await fotos.SalvarAsync(token);
            }
            veiculo.DefinirFoto(foto, null, agora);
            trabalho.Reter(pendencia.TokenPosse, foto, agora);
            auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Veiculo, veiculo.Id, "fotoAtivaId");
            return veiculo;
        }, cancelamento);

    public async Task<Veiculo> RemoverAsync(Guid veiculoId, string? versao, CancellationToken cancelamento)
    {
        if (retomada is not null) await retomada.RetomarAsync(cancelamento);
        return await unidade.ExecutarAsync(async token =>
        {
            var veiculo = await ExigirVeiculoAsync(veiculoId, versao, token);
            if (veiculo.FotoAtivaId is Guid id)
            {
                var foto = await fotos.ConsultarAsync(id, token) ?? throw new CadastroNaoEncontradoException();
                var agora = tempo.GetUtcNow();
                veiculo.RemoverFoto(foto, agora);
                var retencao = await fotos.TrabalhoAsync(id, token) ?? throw new CadastroNaoEncontradoException();
                retencao.AgendarRetencao(foto, agora);
                auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Veiculo, veiculo.Id, "fotoAtivaId");
            }
            return veiculo;
        }, cancelamento);
    }

    private async Task<Veiculo> ExigirVeiculoAsync(Guid id, string? versao, CancellationToken token)
    {
        await sessao.ExigirAsync(token);
        var veiculo = await veiculos.ConsultarAsync(id, token) ?? throw new CadastroNaoEncontradoException();
        VersaoCadastro.Exigir(versao, veiculo.RowVersion);
        if (await alugueis.PossuiAluguelAbertoAsync(id, token))
            throw new ConflitoCadastroException("veiculo_com_aluguel_aberto");
        return veiculo;
    }

    public static ObjetoFoto Objeto(FotoObjeto foto) => new(foto.Chave, foto.Mime, foto.TamanhoBytes, foto.Sha256.ToArray());
}
