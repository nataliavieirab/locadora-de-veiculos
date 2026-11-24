using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public sealed class CasosEdicaoAluguel(IConsultasContratacao consultas, IRepositorioAlugueis repositorio,
    IUnidadeTrabalhoEmpresa unidade, IAuditoriaCadastro auditoria, TimeProvider tempo)
{
    public Task<ResultadoContratacao> AlterarAsync(Guid id, ComandoContratacao comando, string? versao,
        CancellationToken cancelamento)
    {
        comando.Validar();
        comando = comando with { TaxasServicoIds = comando.TaxasServicoIds.ToArray(), Seguros = comando.Seguros.ToArray() };
        return unidade.ExecutarAsync(async token =>
        {
            var aluguel = await repositorio.ConsultarAsync(id, token) ?? throw new CadastroNaoEncontradoException();
            VersaoCadastro.Exigir(versao, aluguel.RowVersion);
            if (aluguel.Estado != EstadoAluguel.EmAberto) throw new ConflitoCadastroException("aluguel_nao_editavel");
            var dados = await consultas.CarregarAsync(comando, token);
            if (await consultas.VeiculoOcupadoAsync(comando.VeiculoId, id, token))
                throw new ConflitoCadastroException("veiculo_indisponivel", "veiculoId");
            var anterior = aluguel.RevisaoAtual;
            aluguel.Editar(dados, tempo.GetUtcNow());
            var campos = new List<string> { "revisao" };
            if (anterior.ClienteId != aluguel.ClienteId) campos.Add("clienteId");
            if (anterior.CondutorId != aluguel.CondutorId) campos.Add("condutorId");
            if (anterior.VeiculoId != aluguel.VeiculoId) campos.Add("veiculoId");
            if (anterior.PlanoId != aluguel.PlanoId) campos.Add("planoId");
            if (anterior.RetornoPrevistoUtc != aluguel.RetornoPrevistoUtc) campos.Add("retornoPrevistoUtc");
            if (anterior.HodometroInicial != aluguel.HodometroInicial) campos.Add("hodometroInicial");
            if (!anterior.Itens.Select(i => (i.Categoria, i.OrigemId)).ToHashSet()
                .SetEquals(aluguel.RevisaoAtual.Itens.Select(i => (i.Categoria, i.OrigemId)))) campos.Add("itens");
            auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Aluguel, id, campos.ToArray());
            var novaVersao = await repositorio.SalvarAsync(aluguel, token);
            return new ResultadoContratacao(id, aluguel.RevisaoAtual, novaVersao);
        }, cancelamento);
    }
}
