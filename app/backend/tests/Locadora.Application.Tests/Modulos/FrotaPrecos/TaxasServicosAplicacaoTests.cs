using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.FrotaPrecos;

[TestClass]
public sealed class TaxasServicosAplicacaoTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [TestMethod]
    [DataRow(ModalidadeTaxa.Fixa)]
    [DataRow(ModalidadeTaxa.Diaria)]
    public async Task CriaAlteraEExcluiComAuditoria(ModalidadeTaxa modalidade)
    {
        var ambiente = new Ambiente();
        var taxa = await ambiente.Casos.CriarAsync(new(" Taxa sintética ", 0m, modalidade), Token);
        Assert.AreEqual("Taxa sintética", taxa.Nome);
        Assert.AreEqual(1, (await ambiente.Casos.ListarAsync(new Paginacao(), Token)).Total);
        await ambiente.Casos.AlterarAsync(taxa.Id, new("Taxa sintética", 15m, modalidade), Versao(taxa), Token);
        Assert.AreEqual(15m, taxa.Preco);
        Assert.AreEqual("preco", string.Join(',', ambiente.Eventos.Last().Campos));
        var quantidade = ambiente.Eventos.Count;
        await ambiente.Casos.AlterarAsync(taxa.Id, new("Taxa sintética", 15m, modalidade), Versao(taxa), Token);
        Assert.AreEqual(quantidade, ambiente.Eventos.Count);
        await ambiente.Casos.ExcluirAsync(taxa.Id, Versao(taxa), Token);
        Assert.IsNotNull(taxa.ExcluidoEmUtc);
        Assert.AreEqual(AcaoCadastro.Exclusao, ambiente.Eventos.Last().Acao);
        Assert.AreEqual(0, (await ambiente.Casos.ListarAsync(new Paginacao(), Token)).Total);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => ambiente.Casos.ConsultarAsync(taxa.Id, Token));
    }

    [TestMethod]
    public async Task PortaControladaBloqueiaAlteracaoEExclusaoSemEfeito()
    {
        var ambiente = new Ambiente();
        var taxa = await ambiente.Casos.CriarAsync(new("Taxa", 5m, ModalidadeTaxa.Fixa), Token);
        ambiente.AluguelAberto = true;
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.AlterarAsync(taxa.Id, new("Outra", 10m, ModalidadeTaxa.Diaria), Versao(taxa), Token));
        Assert.AreEqual("taxa_com_aluguel_aberto", erro.Codigo);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => ambiente.Casos.ExcluirAsync(taxa.Id, Versao(taxa), Token));
        Assert.AreEqual("Taxa", taxa.Nome);
        Assert.AreEqual(5m, taxa.Preco);
        Assert.IsNull(taxa.ExcluidoEmUtc);
        Assert.AreEqual(1, ambiente.Eventos.Count);
    }

    [TestMethod]
    public async Task RejeitaVersaoAusenteEPrecoInvalidoSemAuditoria()
    {
        var ambiente = new Ambiente();
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            ambiente.Casos.CriarAsync(new("Taxa", -1m, ModalidadeTaxa.Fixa), Token));
        Assert.AreEqual(0, ambiente.Eventos.Count);
        var taxa = await ambiente.Casos.CriarAsync(new("Taxa", 5m, ModalidadeTaxa.Fixa), Token);
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            ambiente.Casos.AlterarAsync(taxa.Id, new("Outra", 10m, ModalidadeTaxa.Fixa), null, Token));
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            ambiente.Casos.AlterarAsync(taxa.Id, new("Outra", -1m, ModalidadeTaxa.Fixa), Versao(taxa), Token));
        Assert.AreEqual("Taxa", taxa.Nome);
        Assert.AreEqual(1, ambiente.Eventos.Count);
    }

    private static string Versao(TaxaServico taxa) => VersaoCadastro.Etag(taxa.RowVersion);

    private sealed class Ambiente : IContextoCadastro, IRepositorioTaxasServicos, IUnidadeTrabalhoEmpresa,
        IAuditoriaCadastro, IVinculosAluguelTaxa
    {
        public Guid EmpresaId { get; } = Guid.NewGuid();
        public bool AluguelAberto { get; set; }
        public List<(AcaoCadastro Acao, string[] Campos)> Eventos { get; } = [];
        private readonly List<TaxaServico> _taxas = [];
        public CasosTaxasServicos Casos { get; }
        public Ambiente() => Casos = new(this, this, this, this, this, TimeProvider.System);
        public Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken token) => comando(token);
        public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid id, params string[] campos) => Eventos.Add((acao, campos));
        public Task<bool> PossuiAluguelAbertoAsync(Guid id, CancellationToken token) => Task.FromResult(AluguelAberto);
        public Task<TaxaServico?> ConsultarAsync(Guid id, CancellationToken token) =>
            Task.FromResult(_taxas.SingleOrDefault(item => item.Id == id && item.ExcluidoEmUtc is null));
        public Task<Pagina<TaxaServico>> ListarAsync(Paginacao p, CancellationToken token)
        {
            var ativas = _taxas.Where(item => item.ExcluidoEmUtc is null).ToArray();
            return Task.FromResult(new Pagina<TaxaServico>(ativas, p.Page, p.PageSize, ativas.Length));
        }
        public void Adicionar(TaxaServico taxa)
        {
            typeof(EntidadeCadastro).GetProperty(nameof(EntidadeCadastro.RowVersion))!.SetValue(taxa, new byte[8]);
            _taxas.Add(taxa);
        }
    }
}
