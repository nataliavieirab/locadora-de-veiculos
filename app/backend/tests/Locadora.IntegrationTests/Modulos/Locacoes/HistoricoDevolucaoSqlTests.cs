using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Microsoft.EntityFrameworkCore;
using static Locadora.IntegrationTests.Modulos.Locacoes.PersistenciaDevolucaoSqlTests;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class HistoricoDevolucaoSqlTests
{
    [TestMethod]
    public async Task ListaEConsultaSeparamEstimativaFinalEIncluemOcultosSomentePorOpcao()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        await FecharAsync(cenario, aluguel.Id, await PrepararDadosAsync(cenario));
        var consultas = Consultas(cenario);
        var pagina = await consultas.ListarAsync(new(1, 20), new(EstadoAluguel.Concluido), CancellationToken.None);
        Assert.AreEqual(1, pagina.Total);
        Assert.AreEqual(254m, pagina.Items.Single().TotalConhecido);
        Assert.AreEqual(334m, pagina.Items.Single().TotalFinal);
        Assert.IsFalse(pagina.Items.Single().Oculto);
        var detalhe = await consultas.ConsultarAsync(aluguel.Id, CancellationToken.None);
        Assert.AreEqual(EstadoAluguel.Concluido, detalhe.Estado);
        Assert.AreEqual(334m, detalhe.Devolucao!.TotalFinal);
        await Unidade(cenario).ExecutarAsync(async token =>
        {
            var atual = await ContratosComFinal(cenario.Banco).SingleAsync(token);
            atual.Ocultar(CenarioLocacoesSql.Agora.AddDays(2));
            cenario.Banco.Add(Auditoria(cenario, aluguel.Id, AcaoCadastro.Ocultacao));
            return 0;
        }, CancellationToken.None);
        cenario.Banco.ChangeTracker.Clear();
        Assert.AreEqual(0, (await consultas.ListarAsync(new(1, 20), new(), CancellationToken.None)).Total);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => consultas.ConsultarAsync(aluguel.Id, CancellationToken.None));
        var oculto = await consultas.ConsultarAsync(aluguel.Id, true, CancellationToken.None);
        Assert.IsTrue(oculto.Oculto);
        Assert.AreEqual(334m, oculto.Devolucao!.TotalFinal);
        Assert.IsTrue((await consultas.ListarAsync(new(1, 20), new(IncluirOcultos: true), CancellationToken.None)).Items.Single().Oculto);
        Assert.AreEqual(0, (await consultas.ReferenciasVeiculosAsync(new(1, 20), false, CancellationToken.None)).Total);
        Assert.AreEqual(1, (await consultas.ReferenciasVeiculosAsync(new(1, 20), true, CancellationToken.None)).Total);
    }

    [TestMethod]
    public async Task ReferenciasPaginamTodasAsIdentidadesSemExporFrotaNaoReferenciada()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        await Unidade(cenario).ExecutarAsync(_ =>
        {
            for (var numero = 0; numero < 22; numero++)
            {
                var veiculo = Veiculo.Novo(cenario.Contexto.EmpresaId!.Value, new($"REF{numero:D4}", "Marca", "Azul", $"Modelo {numero:D2}",
                    TipoCombustivel.Gasolina, 50m, 2026, cenario.Dados.Grupo.Id), CenarioLocacoesSql.Agora);
                cenario.Banco.Add(veiculo);
                cenario.Banco.Add(Aluguel.Criar(veiculo.EmpresaId, cenario.Dados with { Veiculo = veiculo }, CenarioLocacoesSql.Agora.AddMinutes(numero)));
            }
            return Task.FromResult(0);
        }, CancellationToken.None);
        var consultas = Consultas(cenario);
        var primeira = await consultas.ReferenciasVeiculosAsync(new(1, 20), false, CancellationToken.None);
        var segunda = await consultas.ReferenciasVeiculosAsync(new(2, 20), false, CancellationToken.None);
        Assert.AreEqual(22, primeira.Total);
        Assert.HasCount(20, primeira.Items);
        Assert.HasCount(2, segunda.Items);
        var todas = primeira.Items.Concat(segunda.Items).ToArray();
        Assert.AreEqual(22, todas.Select(v => v.Id).Distinct().Count());
        Assert.IsFalse(todas.Any(v => v.Id == cenario.Dados.Veiculo.Id));
        CollectionAssert.AreEqual(todas.OrderBy(v => v.Placa).ThenBy(v => v.Id).ToArray(), todas);
        var lista = await consultas.ListarAsync(new(2, 20), new(), CancellationToken.None);
        Assert.AreEqual(22, lista.Total);
        Assert.HasCount(2, lista.Items);
        Assert.IsTrue(lista.Items.All(i => i.TotalFinal is null && i.Estado == EstadoAluguel.EmAberto));
    }

    [TestMethod]
    public async Task VeiculoExcluidoContinuaFiltravelEReferenciasEscolhemSnapshotMaisRecenteSemVazarEmpresa()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(a);
        var dados = await PrepararDadosAsync(a);
        await FecharAsync(a, aluguel.Id, dados);
        await Unidade(a).ExecutarAsync(async token =>
        {
            var veiculo = await a.Banco.Veiculos.SingleAsync(token);
            veiculo.Alterar(new("REF9999", "Outra marca", "Azul", "Snapshot recente", TipoCombustivel.Gasolina,
                50, 2026, a.Dados.Grupo.Id), CenarioLocacoesSql.Agora.AddMinutes(1));
            return 0;
        }, CancellationToken.None);
        var recente = await Unidade(a).ExecutarAsync(async token =>
        {
            var veiculo = await a.Banco.Veiculos.SingleAsync(token);
            var novo = Aluguel.Criar(veiculo.EmpresaId, a.Dados with { Veiculo = veiculo }, CenarioLocacoesSql.Agora.AddMinutes(1));
            a.Banco.Add(novo);
            return novo.Id;
        }, CancellationToken.None);
        await FecharAsync(a, recente, dados);
        await Unidade(a).ExecutarAsync(async token =>
        {
            var veiculo = await a.Banco.Veiculos.SingleAsync(token);
            veiculo.Excluir(CenarioLocacoesSql.Agora.AddDays(2));
            return 0;
        }, CancellationToken.None);
        var consulta = Consultas(a);
        Assert.AreEqual(0, await a.Banco.Veiculos.CountAsync());
        var lista = await consulta.ListarAsync(new(1, 20), new(VeiculoId: a.Dados.Veiculo.Id), CancellationToken.None);
        Assert.AreEqual(2, lista.Total);
        var referencia = (await consulta.ReferenciasVeiculosAsync(new(1, 20), false, CancellationToken.None)).Items.Single();
        Assert.AreEqual("REF9999", referencia.Placa);
        Assert.AreEqual("Snapshot recente", referencia.Modelo);
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var externa = Consultas(b);
        Assert.AreEqual(0, (await externa.ListarAsync(new(1, 20), new(VeiculoId: a.Dados.Veiculo.Id, IncluirOcultos: true), CancellationToken.None)).Total);
        Assert.AreEqual(0, (await externa.ReferenciasVeiculosAsync(new(1, 20), true, CancellationToken.None)).Total);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => externa.ConsultarAsync(aluguel.Id, true, CancellationToken.None));
    }

    private static ConsultasAluguel Consultas(CenarioLocacoesSql cenario)
        => new(cenario.Banco, new RepositorioAlugueis(cenario.Banco));
}
