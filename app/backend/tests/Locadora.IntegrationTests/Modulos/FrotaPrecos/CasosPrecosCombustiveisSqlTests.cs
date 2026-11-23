using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.FrotaPrecos;

[TestClass]
public sealed class CasosPrecosCombustiveisSqlTests
{
    private static readonly CancellationToken Token = CancellationToken.None;
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task PreservaHistoricoESelecionaVigenciaNoRetornoRetroativo()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var primeiro = await ambiente.Casos.RegistrarAsync(TipoCombustivel.Gasolina, 5.123456m, Token);
        ambiente.Relogio.Agora = Agora.AddHours(2);
        var segundo = await ambiente.Casos.RegistrarAsync(TipoCombustivel.Gasolina, 6m, Token);
        var historico = await ambiente.Casos.ListarAsync(TipoCombustivel.Gasolina, Token);

        CollectionAssert.AreEqual(new[] { primeiro.Id, segundo.Id }, historico.Select(item => item.Id).ToArray());
        Assert.AreEqual(5.123456m, historico[0].PrecoUnitario);
        Assert.AreEqual(Agora, historico[0].InicioVigenciaUtc);
        Assert.AreEqual(Agora.AddHours(2), historico[1].InicioVigenciaUtc);
        Assert.AreEqual(segundo.Id, (await ambiente.Casos.VigenteAsync(TipoCombustivel.Gasolina, Token))!.Id);
        Assert.IsNull(await ambiente.Casos.AplicavelAsync(TipoCombustivel.Gasolina, Agora.AddTicks(-1), Token));
        Assert.AreEqual(primeiro.Id, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Gasolina, Agora, Token))!.Id);
        Assert.AreEqual(primeiro.Id, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Gasolina, Agora.AddHours(1), Token))!.Id);
        Assert.AreEqual(segundo.Id, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Gasolina, Agora.AddHours(2), Token))!.Id);
        Assert.AreEqual(segundo.Id, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Gasolina, Agora.AddDays(1), Token))!.Id);
        Assert.AreEqual(primeiro.Id, (await ambiente.Casos.AplicavelAsync(
            TipoCombustivel.Gasolina, Agora.ToOffset(TimeSpan.FromHours(-3)), Token))!.Id);
        Assert.IsNull(await ambiente.Casos.VigenteAsync(TipoCombustivel.Gas, Token));
        Assert.IsNull(await ambiente.Casos.AplicavelAsync(TipoCombustivel.Gas, Agora, Token));

        var eventos = await ambiente.Banco.AuditoriasCadastro.ToListAsync();
        Assert.AreEqual(2, eventos.Count);
        Assert.IsTrue(eventos.All(item => item.Acao == AcaoCadastro.Criacao
            && item.Entidade == TipoCadastro.PrecoCombustivel
            && item.CamposAlterados == "inicioVigenciaUtc,precoUnitario,tipo"
            && item.ContaId == ambiente.Contexto.ContaId && item.CorrelacaoId == ambiente.Contexto.CorrelacaoId));
    }

    [TestMethod]
    public async Task RelogioFixoDesempataPelaSequenciaSqlSemAlterarInstante()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var primeiro = await ambiente.Casos.RegistrarAsync(TipoCombustivel.Gas, 0m, Token);
        var segundo = await ambiente.Casos.RegistrarAsync(TipoCombustivel.Gas, 7m, Token);
        var terceiro = await ambiente.Casos.RegistrarAsync(TipoCombustivel.Gas, 7m, Token);

        Assert.IsTrue(primeiro.Ordem > 0 && segundo.Ordem > primeiro.Ordem && terceiro.Ordem > segundo.Ordem);
        var historico = await ambiente.Casos.ListarAsync(TipoCombustivel.Gas, Token);
        Assert.IsTrue(historico.All(item => item.InicioVigenciaUtc == Agora));
        CollectionAssert.AreEqual(new[] { primeiro.Id, segundo.Id, terceiro.Id }, historico.Select(item => item.Id).ToArray());
        Assert.AreEqual(terceiro.Id, (await ambiente.Casos.VigenteAsync(TipoCombustivel.Gas, Token))!.Id);
        Assert.AreEqual(terceiro.Id, (await ambiente.Casos.AplicavelAsync(TipoCombustivel.Gas, Agora, Token))!.Id);
        Assert.AreEqual(3, await ambiente.Banco.AuditoriasCadastro.CountAsync());

        ambiente.Banco.ChangeTracker.Clear();
        var persistido = await ambiente.Banco.PrecosCombustiveis.SingleAsync(item => item.Id == primeiro.Id);
        ambiente.Banco.Entry(persistido).Property(item => item.PrecoUnitario).CurrentValue = 99m;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ambiente.Banco.SaveChangesAsync());
        ambiente.Banco.ChangeTracker.Clear();
        persistido = await ambiente.Banco.PrecosCombustiveis.SingleAsync(item => item.Id == primeiro.Id);
        ambiente.Banco.Remove(persistido);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ambiente.Banco.SaveChangesAsync());
        ambiente.Banco.ChangeTracker.Clear();
        Assert.AreEqual(0m, (await ambiente.Casos.ListarAsync(TipoCombustivel.Gas, Token))[0].PrecoUnitario);
    }

    [TestMethod]
    public async Task IsolaTiposEEmpresasEDistingueAusenciaDeZero()
    {
        await using var a = await Ambiente.CriarAsync();
        await using var b = await Ambiente.CriarAsync();
        foreach (var tipo in Enum.GetValues<TipoCombustivel>())
        {
            Assert.IsNull(await a.Casos.VigenteAsync(tipo, Token));
            await a.Casos.RegistrarAsync(tipo, 0m, Token);
            await b.Casos.RegistrarAsync(tipo, 8m, Token);
            Assert.AreEqual(0m, (await a.Casos.VigenteAsync(tipo, Token))!.PrecoUnitario);
            Assert.AreEqual(8m, (await b.Casos.VigenteAsync(tipo, Token))!.PrecoUnitario);
        }
        Assert.AreEqual(4, (await a.Casos.ListarAsync(null, Token)).Count);
        Assert.AreEqual(4, (await b.Casos.ListarAsync(null, Token)).Count);
        Assert.IsTrue((await a.Casos.ListarAsync(null, Token)).All(item => item.EmpresaId == a.Contexto.EmpresaId));
        Assert.AreEqual(4, await a.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task ValorInvalidoNaoCriaVigenciaNemAuditoria()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        foreach (var preco in new[] { -1m, 0.0000001m, 10000000000000m })
        {
            await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
                ambiente.Casos.RegistrarAsync(TipoCombustivel.Diesel, preco, Token));
        }
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            ambiente.Casos.RegistrarAsync((TipoCombustivel)99, 1m, Token));
        Assert.AreEqual(0, await ambiente.Banco.PrecosCombustiveis.CountAsync());
        Assert.AreEqual(0, await ambiente.Banco.AuditoriasCadastro.CountAsync());
    }

    private sealed class Relogio : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = CasosPrecosCombustiveisSqlTests.Agora;
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed class Ambiente : IAsyncDisposable
    {
        public ContextoEmpresa Contexto { get; }
        public LocadoraDbContext Banco { get; }
        public Relogio Relogio { get; } = new();
        public CasosPrecosCombustiveis Casos { get; }

        private Ambiente(ContextoEmpresa contexto, LocadoraDbContext banco)
        {
            Contexto = contexto;
            Banco = banco;
            Casos = new(new RepositorioPrecosCombustiveis(banco),
                new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
                new RegistroAuditoriaCadastro(banco, contexto, Relogio), contexto, Relogio);
        }

        public static async Task<Ambiente> CriarAsync()
        {
            var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true, CorrelacaoId = Guid.NewGuid() };
            var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto);
            var empresa = Empresa.Nova();
            var nome = "combustiveis_" + Guid.NewGuid().ToString("N");
            var conta = new UsuarioConta { Id = Guid.NewGuid(), EmpresaId = empresa.Id, UserName = nome, NormalizedUserName = nome.ToUpperInvariant() };
            contexto.EmpresaId = empresa.Id;
            contexto.ContaId = conta.Id;
            banco.AddRange(empresa, conta);
            await banco.SaveChangesAsync();
            contexto.PermiteCriarEmpresa = false;
            return new Ambiente(contexto, banco);
        }

        public ValueTask DisposeAsync() => Banco.DisposeAsync();
    }
}
