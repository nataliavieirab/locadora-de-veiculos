using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.FrotaPrecos;

public sealed class CasosVeiculos(IRepositorioVeiculos repositorio, IRepositorioGruposPlanos grupos,
    IUnidadeTrabalhoEmpresa unidade, IAuditoriaCadastro auditoria, IContextoCadastro contexto,
    IVinculosAluguelVeiculo alugueis, TimeProvider tempo)
{
    public Task<Pagina<Veiculo>> ListarAsync(Paginacao paginacao, Guid? grupoId, CancellationToken cancelamento) =>
        repositorio.ListarAsync(paginacao, grupoId, cancelamento);

    public async Task<Veiculo> ConsultarAsync(Guid id, CancellationToken cancelamento) =>
        await repositorio.ConsultarAsync(id, cancelamento) ?? throw new CadastroNaoEncontradoException();

    public Task<Veiculo> CriarAsync(DadosVeiculo dados, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var veiculo = Veiculo.Novo(contexto.EmpresaId, dados, tempo.GetUtcNow());
            await ExigirGrupoAsync(veiculo.GrupoId, token);
            await ExigirPlacaAsync(veiculo, null, token);
            repositorio.Adicionar(veiculo);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.Veiculo, veiculo.Id,
                "placa", "marca", "cor", "modelo", "combustivel", "capacidadeLitros", "ano", "grupoId");
            return veiculo;
        }, cancelamento);

    public Task<Veiculo> AlterarAsync(Guid id, DadosVeiculo dados, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var veiculo = await ConsultarAsync(id, token);
            VersaoCadastro.Exigir(versao, veiculo.RowVersion);
            await ExigirSemAluguelAsync(id, token);
            var candidato = Veiculo.Novo(contexto.EmpresaId, dados, tempo.GetUtcNow());
            await ExigirGrupoAsync(candidato.GrupoId, token);
            await ExigirPlacaAsync(candidato, id, token);
            var campos = new List<string>();
            if (veiculo.Placa != candidato.Placa) campos.Add("placa");
            if (veiculo.Marca != candidato.Marca) campos.Add("marca");
            if (veiculo.Cor != candidato.Cor) campos.Add("cor");
            if (veiculo.Modelo != candidato.Modelo) campos.Add("modelo");
            if (veiculo.Combustivel != candidato.Combustivel) campos.Add("combustivel");
            if (veiculo.CapacidadeLitros != candidato.CapacidadeLitros) campos.Add("capacidadeLitros");
            if (veiculo.Ano != candidato.Ano) campos.Add("ano");
            if (veiculo.GrupoId != candidato.GrupoId) campos.Add("grupoId");
            if (campos.Count != 0)
            {
                veiculo.Alterar(dados, tempo.GetUtcNow());
                auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Veiculo, id, campos.ToArray());
            }
            return veiculo;
        }, cancelamento);

    public Task<bool> ExcluirAsync(Guid id, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var veiculo = await ConsultarAsync(id, token);
            VersaoCadastro.Exigir(versao, veiculo.RowVersion);
            await ExigirSemAluguelAsync(id, token);
            veiculo.Excluir(tempo.GetUtcNow());
            auditoria.Registrar(AcaoCadastro.Exclusao, TipoCadastro.Veiculo, id, "excluidoEmUtc");
            return true;
        }, cancelamento);

    private async Task ExigirGrupoAsync(Guid id, CancellationToken cancelamento)
    {
        if (await grupos.GrupoAsync(id, cancelamento) is null) throw new CadastroNaoEncontradoException();
    }

    private async Task ExigirPlacaAsync(Veiculo veiculo, Guid? excetoId, CancellationToken cancelamento)
    {
        if (await repositorio.PlacaEmUsoAsync(veiculo.PlacaNormalizada, excetoId, cancelamento))
            throw new ConflitoCadastroException("cadastro_duplicado", "placa");
    }

    private async Task ExigirSemAluguelAsync(Guid id, CancellationToken cancelamento)
    {
        if (await alugueis.PossuiAluguelAbertoAsync(id, cancelamento))
            throw new ConflitoCadastroException("veiculo_com_aluguel_aberto");
    }
}
