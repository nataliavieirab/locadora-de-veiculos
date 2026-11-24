using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using static Locadora.IntegrationTests.Modulos.Locacoes.PersistenciaDevolucaoSqlTests;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class OcultacaoDevolucaoSqlTests
{
    [TestMethod]
    public async Task ExigeVersaoRejeitaAbertoEEmpresaExternaSemEfeitos()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(a);
        var casos = Casos(a.Banco, a.Contexto);
        var ausente = await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.OcultarAsync(aluguel.Id, null, CancellationToken.None));
        Assert.IsTrue(ausente.Ausente);
        var divergente = await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.OcultarAsync(aluguel.Id, "\"desatualizada\"", CancellationToken.None));
        Assert.IsFalse(divergente.Ausente);
        var aberto = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.OcultarAsync(aluguel.Id, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None));
        Assert.AreEqual("aluguel_nao_ocultavel", aberto.Codigo);
        await using var b = await CenarioLocacoesSql.CriarAsync();
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            Casos(b.Banco, b.Contexto).OcultarAsync(aluguel.Id, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None));
        Assert.AreEqual(1, await a.Banco.Alugueis.CountAsync());
        Assert.AreEqual(0, await a.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task OcultacaoPreservaFinalENovaOcupacaoRepeticaoExigeVersaoAtualESemNovoEvento()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        var final = await FecharAsync(cenario, aluguel.Id, await PrepararDadosAsync(cenario));
        var atual = await new RepositorioAlugueis(cenario.Banco).ConsultarIncluindoOcultoAsync(aluguel.Id, CancellationToken.None);
        var anterior = VersaoCadastro.Etag(atual!.RowVersion);
        var nova = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto, new ConclusaoDevolucaoSqlTests.Relogio())
            .CriarAsync(EstimativasSelecoesTests.Comando(cenario) with
            { RetornoPrevisto = CenarioLocacoesSql.Agora.AddDays(4) }, Guid.NewGuid(), CancellationToken.None);
        var casos = Casos(cenario.Banco, cenario.Contexto);
        await casos.OcultarAsync(aluguel.Id, anterior, CancellationToken.None);
        cenario.Banco.ChangeTracker.Clear();
        Assert.AreEqual(1, await cenario.Banco.Alugueis.CountAsync());
        var oculto = await new RepositorioAlugueis(cenario.Banco).ConsultarIncluindoOcultoAsync(aluguel.Id, CancellationToken.None);
        Assert.IsNotNull(oculto!.ExcluidoEmUtc);
        var versaoAtual = VersaoCadastro.Etag(oculto.RowVersion);
        Assert.AreNotEqual(anterior, versaoAtual);
        var divergente = await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.OcultarAsync(aluguel.Id, anterior, CancellationToken.None));
        Assert.IsFalse(divergente.Ausente);
        await casos.OcultarAsync(aluguel.Id, versaoAtual, CancellationToken.None);
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Ocultacao));
        Assert.AreEqual(1, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(final.Id, (await cenario.Banco.DemonstrativosFinais.SingleAsync()).Id);
        Assert.AreEqual(EstadoAluguel.EmAberto, (await cenario.Banco.Alugueis.AsNoTracking().SingleAsync(a => a.Id == nova.AluguelId)).Estado);
        Assert.IsTrue(await new ConsultasContratacao(cenario.Banco).VeiculoOcupadoAsync(aluguel.VeiculoId, null, CancellationToken.None));
        var consulta = new ConsultasAluguel(cenario.Banco, new RepositorioAlugueis(cenario.Banco));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => consulta.ConsultarAsync(aluguel.Id, CancellationToken.None));
        Assert.IsTrue((await consulta.ConsultarAsync(aluguel.Id, true, CancellationToken.None)).Oculto);
    }

    [TestMethod]
    public async Task FalhaAposSaveReverteOcultacaoEAuditoriaSemAlterarFinalOuOcupacao()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        await FecharAsync(cenario, aluguel.Id, await PrepararDadosAsync(cenario));
        var lido = await new RepositorioAlugueis(cenario.Banco).ConsultarIncluindoOcultoAsync(aluguel.Id, CancellationToken.None);
        var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>().UseSqlServer(AmbienteIntegracao.Conexao)
            .AddInterceptors(new GuardaEscritaEmpresa(cenario.Contexto), new FalhaDepoisSave()).Options;
        await using var banco = new LocadoraDbContext(opcoes, cenario.Contexto);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            Casos(banco, cenario.Contexto).OcultarAsync(aluguel.Id, VersaoCadastro.Etag(lido!.RowVersion), CancellationToken.None));
        cenario.Banco.ChangeTracker.Clear();
        var preservado = await ContratosComFinal(cenario.Banco).SingleAsync();
        Assert.IsNull(preservado.ExcluidoEmUtc);
        CollectionAssert.AreEqual(lido!.RowVersion, preservado.RowVersion);
        Assert.AreEqual(334m, preservado.Devolucao!.TotalFinal);
        Assert.AreEqual(0, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Ocultacao));
        Assert.IsFalse(await new ConsultasContratacao(cenario.Banco).VeiculoOcupadoAsync(aluguel.VeiculoId, null, CancellationToken.None));
    }

    internal static CasosOcultacaoAluguel Casos(LocadoraDbContext banco, ContextoEmpresa contexto)
        => new(new RepositorioAlugueis(banco),
            new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
            new RegistroAuditoriaCadastro(banco, contexto, new ConclusaoDevolucaoSqlTests.Relogio()),
            new ConclusaoDevolucaoSqlTests.Relogio());

    private sealed class FalhaDepoisSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Falha sintética da ocultação antes do commit.");
    }
}
