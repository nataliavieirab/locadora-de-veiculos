using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.FrotaPrecos;

public sealed record DadosTaxaServico(string Nome, decimal Preco, ModalidadeTaxa Modalidade);

public sealed class CasosTaxasServicos(IRepositorioTaxasServicos repositorio, IUnidadeTrabalhoEmpresa unidade,
    IAuditoriaCadastro auditoria, IContextoCadastro contexto, IVinculosAluguelTaxa alugueis, TimeProvider tempo)
{
    public Task<Pagina<TaxaServico>> ListarAsync(Paginacao paginacao, CancellationToken cancelamento) =>
        repositorio.ListarAsync(paginacao, cancelamento);

    public async Task<TaxaServico> ConsultarAsync(Guid id, CancellationToken cancelamento) =>
        await repositorio.ConsultarAsync(id, cancelamento) ?? throw new CadastroNaoEncontradoException();

    public Task<TaxaServico> CriarAsync(DadosTaxaServico dados, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(token =>
        {
            var taxa = TaxaServico.Nova(contexto.EmpresaId, dados.Nome, dados.Preco, dados.Modalidade, tempo.GetUtcNow());
            repositorio.Adicionar(taxa);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.TaxaServico, taxa.Id, "nome", "preco", "modalidade");
            return Task.FromResult(taxa);
        }, cancelamento);

    public Task<TaxaServico> AlterarAsync(Guid id, DadosTaxaServico dados, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var taxa = await ConsultarAsync(id, token);
            VersaoCadastro.Exigir(versao, taxa.RowVersion);
            await ExigirSemAluguelAsync(id, token);
            var candidato = TaxaServico.Nova(contexto.EmpresaId, dados.Nome, dados.Preco, dados.Modalidade, tempo.GetUtcNow());
            var campos = new List<string>();
            if (taxa.Nome != candidato.Nome) campos.Add("nome");
            if (taxa.Preco != candidato.Preco) campos.Add("preco");
            if (taxa.Modalidade != candidato.Modalidade) campos.Add("modalidade");
            if (campos.Count != 0)
            {
                taxa.Alterar(dados.Nome, dados.Preco, dados.Modalidade, tempo.GetUtcNow());
                auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.TaxaServico, id, campos.ToArray());
            }
            return taxa;
        }, cancelamento);

    public Task<bool> ExcluirAsync(Guid id, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var taxa = await ConsultarAsync(id, token);
            VersaoCadastro.Exigir(versao, taxa.RowVersion);
            await ExigirSemAluguelAsync(id, token);
            taxa.Excluir(tempo.GetUtcNow());
            auditoria.Registrar(AcaoCadastro.Exclusao, TipoCadastro.TaxaServico, id, "excluidoEmUtc");
            return true;
        }, cancelamento);

    private async Task ExigirSemAluguelAsync(Guid id, CancellationToken cancelamento)
    {
        if (await alugueis.PossuiAluguelAbertoAsync(id, cancelamento))
            throw new ConflitoCadastroException("taxa_com_aluguel_aberto");
    }
}
