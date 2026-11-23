using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.FrotaPrecos;

[TestClass]
public sealed class PrecosCombustiveisAplicacaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task RegistrarAcrescentaVigenciasSemEditarAnteriores()
    {
        var relogio = new Relogio(Agora);
        var ambiente = new Ambiente(relogio);
        var primeiro = await ambiente.Casos.RegistrarAsync(TipoCombustivel.Gasolina, 5m, CancellationToken.None);
        relogio.Agora = Agora.AddMinutes(5);
        var segundo = await ambiente.Casos.RegistrarAsync(TipoCombustivel.Gasolina, 6m, CancellationToken.None);

        var historico = await ambiente.Casos.ListarAsync(TipoCombustivel.Gasolina, CancellationToken.None);
        Assert.AreEqual(2, historico.Count);
        Assert.AreEqual(primeiro.Id, historico[0].Id);
        Assert.AreEqual(5m, historico[0].PrecoUnitario);
        Assert.AreEqual(6m, historico[1].PrecoUnitario);
        Assert.IsTrue(segundo.Ordem > primeiro.Ordem);
        Assert.AreEqual(segundo.Id, (await ambiente.Casos.VigenteAsync(TipoCombustivel.Gasolina, CancellationToken.None))!.Id);
        Assert.AreEqual(2, ambiente.Auditoria.Eventos.Count);
        Assert.IsTrue(ambiente.Auditoria.Eventos.All(item => item.Acao == AcaoCadastro.Criacao
            && item.Entidade == TipoCadastro.PrecoCombustivel));
        Assert.IsTrue(ambiente.Auditoria.Eventos.All(item => item.Campos.SequenceEqual(
            new[] { "tipo", "precoUnitario", "inicioVigenciaUtc" })));
    }

    [TestMethod]
    public async Task AplicavelSelecionaUltimaVigenciaAteOMomentoESinalizaAusencia()
    {
        var relogio = new Relogio(Agora);
        var ambiente = new Ambiente(relogio);
        await ambiente.Casos.RegistrarAsync(TipoCombustivel.Diesel, 5m, CancellationToken.None);
        relogio.Agora = Agora.AddHours(2);
        await ambiente.Casos.RegistrarAsync(TipoCombustivel.Diesel, 6m, CancellationToken.None);

        Assert.IsNull(await ambiente.Casos.AplicavelAsync(TipoCombustivel.Diesel, Agora.AddMinutes(-1), CancellationToken.None));
        Assert.AreEqual(5m, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Diesel, Agora, CancellationToken.None))!.PrecoUnitario);
        Assert.AreEqual(5m, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Diesel, Agora.AddHours(1), CancellationToken.None))!.PrecoUnitario);
        Assert.AreEqual(6m, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Diesel, Agora.AddHours(2), CancellationToken.None))!.PrecoUnitario);
        Assert.AreEqual(6m, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Diesel, Agora.AddHours(3), CancellationToken.None))!.PrecoUnitario);
        Assert.IsNull(await ambiente.Casos.AplicavelAsync(TipoCombustivel.Alcool, Agora, CancellationToken.None));
    }

    [TestMethod]
    public async Task EmpateDeInstanteDesempataPelaOrdemSerial()
    {
        var ambiente = new Ambiente(new Relogio(Agora));
        var primeiro = await ambiente.Casos.RegistrarAsync(TipoCombustivel.Gas, 5m, CancellationToken.None);
        var segundo = await ambiente.Casos.RegistrarAsync(TipoCombustivel.Gas, 6m, CancellationToken.None);

        Assert.AreEqual(primeiro.InicioVigenciaUtc, segundo.InicioVigenciaUtc);
        var historico = await ambiente.Casos.ListarAsync(TipoCombustivel.Gas, CancellationToken.None);
        Assert.AreEqual(primeiro.Id, historico[0].Id);
        Assert.AreEqual(segundo.Id, historico[1].Id);
        Assert.AreEqual(6m, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Gas, Agora, CancellationToken.None))!.PrecoUnitario);
    }

    [TestMethod]
    public async Task PrecoNegativoNaoCriaVigenciaNemAuditoria()
    {
        var ambiente = new Ambiente(new Relogio(Agora));
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            ambiente.Casos.RegistrarAsync(TipoCombustivel.Gasolina, -1m, CancellationToken.None));
        Assert.AreEqual(0, (await ambiente.Casos.ListarAsync(null, CancellationToken.None)).Count);
        Assert.AreEqual(0, ambiente.Auditoria.Eventos.Count);
    }

    private sealed class Relogio(DateTimeOffset agora) : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = agora;
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed class Ambiente : IContextoCadastro
    {
        public Guid EmpresaId { get; } = Guid.NewGuid();
        public Repositorio Repositorio { get; } = new();
        public Auditoria Auditoria { get; } = new();
        public CasosPrecosCombustiveis Casos { get; }

        public Ambiente(TimeProvider relogio) =>
            Casos = new(Repositorio, new Unidade(), Auditoria, this, relogio);
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

    private sealed class Repositorio : IRepositorioPrecosCombustiveis
    {
        private long _ordem;
        private readonly List<PrecoCombustivel> _precos = [];

        public Task<IReadOnlyList<PrecoCombustivel>> ListarAsync(TipoCombustivel? tipo, CancellationToken cancelamento) =>
            Task.FromResult<IReadOnlyList<PrecoCombustivel>>(_precos
                .Where(item => tipo == null || item.Tipo == tipo)
                .OrderBy(item => item.Tipo)
                .ThenBy(item => item.InicioVigenciaUtc)
                .ThenBy(item => item.Ordem)
                .ThenBy(item => item.Id)
                .ToArray());

        public Task<PrecoCombustivel?> VigenteAsync(TipoCombustivel tipo, CancellationToken cancelamento) =>
            Task.FromResult(_precos.Where(item => item.Tipo == tipo)
                .OrderByDescending(item => item.InicioVigenciaUtc)
                .ThenByDescending(item => item.Ordem)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault());

        public Task<PrecoCombustivel?> AplicavelAsync(TipoCombustivel tipo, DateTimeOffset momento,
            CancellationToken cancelamento) =>
            Task.FromResult(_precos.Where(item => item.Tipo == tipo && item.InicioVigenciaUtc <= momento)
                .OrderByDescending(item => item.InicioVigenciaUtc)
                .ThenByDescending(item => item.Ordem)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault());

        public void Adicionar(PrecoCombustivel preco)
        {
            typeof(PrecoCombustivel).GetProperty(nameof(PrecoCombustivel.Ordem))!
                .SetValue(preco, ++_ordem);
            _precos.Add(preco);
        }
    }
}
