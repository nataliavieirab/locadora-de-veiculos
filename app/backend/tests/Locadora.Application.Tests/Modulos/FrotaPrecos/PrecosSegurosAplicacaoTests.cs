using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.FrotaPrecos;

[TestClass]
public sealed class PrecosSegurosAplicacaoTests
{
    [TestMethod]
    public async Task AusenciaDeConfiguracaoNaoSeConfundeComPrecoZero()
    {
        var ambiente = new Ambiente();
        Assert.AreEqual(0, (await ambiente.Casos.ListarAsync(CancellationToken.None)).Count);
        Assert.IsNull(await ambiente.Casos.SeguroAsync(TipoSeguro.Cliente, CancellationToken.None));

        var cliente = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 0m, null, CancellationToken.None);
        Assert.AreEqual(0m, cliente.PrecoDiario);
        Assert.IsNull(await ambiente.Casos.SeguroAsync(TipoSeguro.Terceiros, CancellationToken.None));

        var terceiros = await ambiente.Casos.SalvarAsync(TipoSeguro.Terceiros, 7m, null, CancellationToken.None);
        Assert.AreEqual(7m, terceiros.PrecoDiario);
        Assert.AreEqual(0m, (await ambiente.Casos.SeguroAsync(TipoSeguro.Cliente, CancellationToken.None))!.PrecoDiario);
        Assert.AreEqual(2, (await ambiente.Casos.ListarAsync(CancellationToken.None)).Count);
        Assert.AreNotEqual(cliente.Id, terceiros.Id);
    }

    [TestMethod]
    public async Task AlterarUmSeguroNaoAlteraOutroEAuditaSomenteCampoAlterado()
    {
        var ambiente = new Ambiente();
        var cliente = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 5m, null, CancellationToken.None);
        await ambiente.Casos.SalvarAsync(TipoSeguro.Terceiros, 9m, null, CancellationToken.None);
        ambiente.Auditoria.Eventos.Clear();

        var alterado = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 12m,
            VersaoCadastro.Etag(cliente.RowVersion), CancellationToken.None);

        Assert.AreEqual(12m, alterado.PrecoDiario);
        Assert.AreEqual(9m, (await ambiente.Casos.SeguroAsync(TipoSeguro.Terceiros, CancellationToken.None))!.PrecoDiario);
        Assert.AreEqual(1, ambiente.Auditoria.Eventos.Count);
        Assert.AreEqual(AcaoCadastro.Alteracao, ambiente.Auditoria.Eventos[0].Acao);
        Assert.AreEqual(TipoCadastro.PrecoSeguro, ambiente.Auditoria.Eventos[0].Entidade);
        Assert.AreEqual("precoDiario", string.Join(',', ambiente.Auditoria.Eventos[0].Campos));
    }

    [TestMethod]
    public async Task RepetirOMesmoPrecoNaoCriaVersaoNemAuditoria()
    {
        var ambiente = new Ambiente();
        var cliente = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 5m, null, CancellationToken.None);
        var versao = cliente.RowVersion;
        ambiente.Auditoria.Eventos.Clear();

        var repetido = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 5m,
            VersaoCadastro.Etag(versao), CancellationToken.None);

        CollectionAssert.AreEqual(versao, repetido.RowVersion);
        Assert.AreEqual(0, ambiente.Auditoria.Eventos.Count);
        Assert.AreEqual(1, ambiente.Repositorio.Seguros.Count);
    }

    [TestMethod]
    public async Task AlteracaoExigeVersaoEPrecoNegativoNaoCriaConfiguracao()
    {
        var ambiente = new Ambiente();
        var cliente = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 5m, null, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 6m, null, CancellationToken.None));
        var versaoDesconhecida = new byte[8];
        versaoDesconhecida[0] = 1;
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 6m, VersaoCadastro.Etag(versaoDesconhecida), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            ambiente.Casos.SalvarAsync(TipoSeguro.Terceiros, -1m, null, CancellationToken.None));

        Assert.AreEqual(5m, cliente.PrecoDiario);
        Assert.IsNull(await ambiente.Casos.SeguroAsync(TipoSeguro.Terceiros, CancellationToken.None));
        Assert.AreEqual(1, ambiente.Auditoria.Eventos.Count);
        Assert.AreEqual(AcaoCadastro.Criacao, ambiente.Auditoria.Eventos[0].Acao);
    }

    private sealed class Ambiente : IContextoCadastro
    {
        public Guid EmpresaId { get; } = Guid.NewGuid();
        public Repositorio Repositorio { get; } = new();
        public Auditoria Auditoria { get; } = new();
        public CasosPrecosSeguros Casos { get; }

        public Ambiente() =>
            Casos = new(Repositorio, new Unidade(), Auditoria, this, TimeProvider.System);
    }

    private sealed class Unidade : IUnidadeTrabalhoEmpresa
    {
        public Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento) =>
            comando(cancelamento);
    }

    private sealed class Auditoria : IAuditoriaCadastro
    {
        public List<(AcaoCadastro Acao, TipoCadastro Entidade, string[] Campos)> Eventos { get; } = [];

        public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid id, params string[] campos) =>
            Eventos.Add((acao, entidade, campos));
    }

    private sealed class Repositorio : IRepositorioPrecosSeguros
    {
        public List<PrecoSeguro> Seguros { get; } = [];

        public Task<IReadOnlyList<PrecoSeguro>> ListarSegurosAsync(CancellationToken cancelamento) =>
            Task.FromResult<IReadOnlyList<PrecoSeguro>>(Seguros.OrderBy(item => item.Tipo).ToArray());

        public Task<PrecoSeguro?> SeguroAsync(TipoSeguro tipo, CancellationToken cancelamento) =>
            Task.FromResult(Seguros.SingleOrDefault(item => item.Tipo == tipo));

        public void Adicionar(PrecoSeguro seguro)
        {
            typeof(EntidadeCadastro).GetProperty(nameof(EntidadeCadastro.RowVersion))!
                .SetValue(seguro, new byte[8]);
            Seguros.Add(seguro);
        }
    }
}
