using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using static Locadora.IntegrationTests.Modulos.Locacoes.PersistenciaDevolucaoSqlTests;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class RecuperacaoDevolucaoSqlTests
{
    [TestMethod]
    public async Task MesmaChaveDistingueOperacoesEDevolveCriacaoOriginalAposEditarEConcluir()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        var criacao = await cenario.Banco.ComandosIdempotentes.AsNoTracking().SingleAsync();
        await Unidade(cenario).ExecutarAsync(async token =>
        {
            var atual = await CenarioLocacoesSql.Contratos(cenario.Banco).SingleAsync(token);
            atual.Editar(cenario.Dados with { HodometroInicial = 1001 }, CenarioLocacoesSql.Agora.AddMinutes(1));
            return 0;
        }, CancellationToken.None);
        var final = await FecharAsync(cenario, aluguel.Id, await PrepararDadosAsync(cenario), criacao.Chave);
        cenario.Banco.ChangeTracker.Clear();
        var repositorio = new RepositorioAlugueis(cenario.Banco);
        var original = await repositorio.ComandoAsync(criacao.ContaId, criacao.Chave, CancellationToken.None);
        var devolucao = await repositorio.ComandoAsync(criacao.ContaId, "devolver-aluguel", criacao.Chave, CancellationToken.None);
        Assert.IsNotNull(original);
        Assert.IsNotNull(devolucao);
        Assert.AreNotEqual(original.Id, devolucao.Id);
        Assert.IsNull(original.DemonstrativoFinalId);
        Assert.AreEqual(final.Id, devolucao.DemonstrativoFinalId);
        var revisaoOriginal = await repositorio.RevisaoAsync(original.RevisaoId, CancellationToken.None);
        Assert.AreEqual(1, revisaoOriginal.Numero);
        Assert.AreEqual(1000, revisaoOriginal.HodometroInicial);
        CollectionAssert.AreEqual(criacao.VersaoOriginal, original.VersaoOriginal);
        var finalLido = await repositorio.FinalAsync(devolucao.DemonstrativoFinalId!.Value, CancellationToken.None);
        Assert.IsNotNull(finalLido);
        Assert.AreEqual(2, (await repositorio.ConsultarIncluindoOcultoAsync(aluguel.Id, CancellationToken.None))!.NumeroRevisaoAtual);
        Assert.AreEqual(final.TotalFinal, finalLido.TotalFinal);
        Assert.AreEqual(2, await cenario.Banco.ComandosIdempotentes.CountAsync());
    }

    [TestMethod]
    public async Task RecuperacaoAposOcultarPreservaTenantAtorEReferenciaDoPai()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(a);
        var final = await FecharAsync(a, aluguel.Id, await PrepararDadosAsync(a));
        var comando = await a.Banco.ComandosIdempotentes.AsNoTracking().SingleAsync(c => c.Operacao == "devolver-aluguel");
        await Unidade(a).ExecutarAsync(async token =>
        {
            var atual = await ContratosComFinal(a.Banco).SingleAsync(token);
            Assert.IsTrue(atual.Ocultar(CenarioLocacoesSql.Agora.AddDays(3)));
            a.Banco.Add(Auditoria(a, atual.Id, AcaoCadastro.Ocultacao));
            return 0;
        }, CancellationToken.None);
        a.Banco.ChangeTracker.Clear();
        var repositorio = new RepositorioAlugueis(a.Banco);
        Assert.IsNull(await repositorio.ConsultarAsync(aluguel.Id, CancellationToken.None));
        var oculto = await repositorio.ConsultarIncluindoOcultoAsync(aluguel.Id, CancellationToken.None);
        Assert.IsNotNull(oculto);
        Assert.IsNotNull(oculto.ExcluidoEmUtc);
        Assert.IsNotNull(await repositorio.FinalAsync(final.Id, CancellationToken.None));
        var recuperado = await repositorio.ComandoAsync(comando.ContaId, "devolver-aluguel", comando.Chave, CancellationToken.None);
        Assert.AreEqual(final.Id, recuperado!.DemonstrativoFinalId);
        Assert.IsFalse(oculto.RowVersion.SequenceEqual(recuperado.VersaoOriginal));
        Assert.IsNull(await repositorio.ComandoAsync(Guid.NewGuid(), "devolver-aluguel", comando.Chave, CancellationToken.None));
        await using var b = await CenarioLocacoesSql.CriarAsync();
        await using var semContexto = AmbienteIntegracao.CriarContexto();
        foreach (var banco in new[] { b.Banco, semContexto })
        {
            var externo = new RepositorioAlugueis(banco);
            Assert.IsNull(await externo.ConsultarIncluindoOcultoAsync(aluguel.Id, CancellationToken.None));
            Assert.IsNull(await externo.FinalAsync(final.Id, CancellationToken.None));
            Assert.IsNull(await externo.ComandoAsync(comando.ContaId, "devolver-aluguel", comando.Chave, CancellationToken.None));
        }
        Assert.AreEqual(1, await a.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(1, await a.Banco.AuditoriasCadastro.CountAsync(c => c.Acao == AcaoCadastro.Devolucao));
    }
}
