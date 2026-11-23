using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
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
public sealed class CasosPrecosSegurosSqlTests
{
    private static readonly CancellationToken Token = CancellationToken.None;
    private static string Versao(PrecoSeguro seguro) => VersaoCadastro.Etag(seguro.RowVersion);

    [TestMethod]
    public async Task CriaDoisSegurosIndependentesEDistingueAusenciaDeZero()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        Assert.IsNull(await ambiente.Casos.SeguroAsync(TipoSeguro.Cliente, Token));
        Assert.AreEqual(0, (await ambiente.Casos.ListarAsync(Token)).Count);

        var cliente = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 0m, null, Token);
        Assert.AreEqual(0m, cliente.PrecoDiario);
        Assert.AreEqual(8, cliente.RowVersion.Length);
        Assert.IsNull(await ambiente.Casos.SeguroAsync(TipoSeguro.Terceiros, Token));

        var terceiros = await ambiente.Casos.SalvarAsync(TipoSeguro.Terceiros, 7m, null, Token);
        Assert.AreEqual(7m, terceiros.PrecoDiario);
        Assert.AreEqual(0m, (await ambiente.Casos.SeguroAsync(TipoSeguro.Cliente, Token))!.PrecoDiario);
        Assert.AreEqual(2, (await ambiente.Casos.ListarAsync(Token)).Count);
        Assert.AreNotEqual(cliente.Id, terceiros.Id);

        var eventos = await ambiente.Banco.AuditoriasCadastro.OrderBy(item => item.EntidadeId).ToListAsync();
        Assert.AreEqual(2, eventos.Count);
        Assert.IsTrue(eventos.All(item => item.Acao == AcaoCadastro.Criacao && item.Entidade == TipoCadastro.PrecoSeguro));
        Assert.IsTrue(eventos.All(item => item.CamposAlterados == "precoDiario,tipo"));
        Assert.IsTrue(eventos.All(item => item.ContaId == ambiente.Contexto.ContaId
            && item.CorrelacaoId == ambiente.Contexto.CorrelacaoId));
    }

    [TestMethod]
    public async Task AlteraSomenteUmSeguroEAuditaSomenteCampoAlterado()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var cliente = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 5m, null, Token);
        await ambiente.Casos.SalvarAsync(TipoSeguro.Terceiros, 9m, null, Token);
        var antes = await ambiente.Banco.AuditoriasCadastro.CountAsync();

        var alterado = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 12m, Versao(cliente), Token);
        Assert.AreEqual(12m, alterado.PrecoDiario);
        Assert.IsFalse(cliente.RowVersion.SequenceEqual(alterado.RowVersion));
        Assert.AreEqual(9m, (await ambiente.Casos.SeguroAsync(TipoSeguro.Terceiros, Token))!.PrecoDiario);
        var evento = await ambiente.Banco.AuditoriasCadastro.SingleAsync(item =>
            item.EntidadeId == cliente.Id && item.Acao == AcaoCadastro.Alteracao);
        Assert.AreEqual("precoDiario", evento.CamposAlterados);
        Assert.AreEqual(antes + 1, await ambiente.Banco.AuditoriasCadastro.CountAsync());

        var repetido = await ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 12m, Versao(alterado), Token);
        CollectionAssert.AreEqual(alterado.RowVersion, repetido.RowVersion);
        Assert.AreEqual(antes + 1, await ambiente.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(1, await ambiente.Banco.PrecosSeguros.CountAsync(item => item.Tipo == TipoSeguro.Cliente));

        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 13m, null, Token));
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            ambiente.Casos.SalvarAsync(TipoSeguro.Cliente, 13m, VersaoCadastro.Etag(new byte[8]), Token));
        Assert.AreEqual(12m, (await ambiente.Casos.SeguroAsync(TipoSeguro.Cliente, Token))!.PrecoDiario);
    }

    [TestMethod]
    public async Task IsolaConfiguracoesEntreEmpresasEPreservaValorCapturado()
    {
        await using var a = await Ambiente.CriarAsync();
        await using var b = await Ambiente.CriarAsync();
        var seguroA = await a.Casos.SalvarAsync(TipoSeguro.Cliente, 5m, null, Token);
        var seguroB = await b.Casos.SalvarAsync(TipoSeguro.Cliente, 9m, null, Token);
        var capturadoParaContratoFuturo = seguroA.PrecoDiario;

        Assert.AreEqual(5m, (await a.Casos.SeguroAsync(TipoSeguro.Cliente, Token))!.PrecoDiario);
        Assert.AreEqual(9m, (await b.Casos.SeguroAsync(TipoSeguro.Cliente, Token))!.PrecoDiario);
        Assert.AreEqual(1, (await a.Casos.ListarAsync(Token)).Count);
        Assert.AreEqual(1, (await b.Casos.ListarAsync(Token)).Count);
        Assert.AreNotEqual(seguroA.Id, seguroB.Id);

        await a.Casos.SalvarAsync(TipoSeguro.Cliente, 15m, Versao(seguroA), Token);
        Assert.AreEqual(15m, (await a.Casos.SeguroAsync(TipoSeguro.Cliente, Token))!.PrecoDiario);
        Assert.AreEqual(9m, (await b.Casos.SeguroAsync(TipoSeguro.Cliente, Token))!.PrecoDiario);
        Assert.AreEqual(5m, capturadoParaContratoFuturo);
        Assert.AreEqual(1, await a.Banco.AuditoriasCadastro.CountAsync(item => item.Acao == AcaoCadastro.Alteracao));
        Assert.AreEqual(0, await b.Banco.AuditoriasCadastro.CountAsync(item => item.Acao == AcaoCadastro.Alteracao));
    }

    [TestMethod]
    public async Task PrecoNegativoNaoPersisteNemAudita()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            ambiente.Casos.SalvarAsync(TipoSeguro.Terceiros, -1m, null, Token));
        Assert.IsNull(await ambiente.Casos.SeguroAsync(TipoSeguro.Terceiros, Token));
        Assert.AreEqual(0, await ambiente.Banco.PrecosSeguros.CountAsync());
        Assert.AreEqual(0, await ambiente.Banco.AuditoriasCadastro.CountAsync());
    }

    private sealed class Ambiente : IAsyncDisposable
    {
        public ContextoEmpresa Contexto { get; }
        public LocadoraDbContext Banco { get; }
        public CasosPrecosSeguros Casos { get; }

        private Ambiente(ContextoEmpresa contexto, LocadoraDbContext banco)
        {
            Contexto = contexto;
            Banco = banco;
            var auditoria = new RegistroAuditoriaCadastro(banco, contexto, TimeProvider.System);
            Casos = new(new RepositorioPrecosSeguros(banco),
                new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
                auditoria, contexto, TimeProvider.System);
        }

        public static async Task<Ambiente> CriarAsync()
        {
            var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true, CorrelacaoId = Guid.NewGuid() };
            var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto);
            var empresa = Empresa.Nova();
            var nome = "seguros_" + Guid.NewGuid().ToString("N");
            var conta = new UsuarioConta
            {
                Id = Guid.NewGuid(), EmpresaId = empresa.Id, UserName = nome, NormalizedUserName = nome.ToUpperInvariant(),
            };
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
