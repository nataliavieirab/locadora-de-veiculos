using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Modulos.Locacoes;
using Microsoft.EntityFrameworkCore;
using static Locadora.IntegrationTests.Modulos.Locacoes.PersistenciaDevolucaoSqlTests;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class PreviaDevolucaoSqlTests
{
    [TestMethod]
    public async Task PreviaNaoGravaEUsaUltimaVigenciaAplicavelComDesempateSerial()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        var dados = await PrepararDadosAsync(cenario);
        var anterior = dados.Combustivel;
        var empate = PrecoCombustivel.Novo(aluguel.EmpresaId, TipoCombustivel.Gasolina, 7m, anterior.InicioVigenciaUtc);
        var futuro = PrecoCombustivel.Novo(aluguel.EmpresaId, TipoCombustivel.Gasolina, 99m, dados.RetornoReal.AddTicks(1));
        await Unidade(cenario).ExecutarAsync(_ =>
        {
            cenario.Banco.AddRange(empate, futuro);
            return Task.FromResult(0);
        }, CancellationToken.None);
        var casos = Casos(cenario);
        var comando = Comando(dados);
        var versao = VersaoCadastro.Etag(aluguel.RowVersion);
        var previa = await casos.PreviaAsync(aluguel.Id, comando, versao, CancellationToken.None);
        Assert.AreEqual(empate.Id, previa.VersoesAceitas.CombustivelId);
        Assert.AreEqual(344m, previa.Calculo.TotalFinal);
        var outraFutura = PrecoCombustivel.Novo(aluguel.EmpresaId, TipoCombustivel.Gasolina, 101m, futuro.InicioVigenciaUtc.AddTicks(1));
        await Unidade(cenario).ExecutarAsync(_ => { cenario.Banco.Add(outraFutura); return Task.FromResult(0); }, CancellationToken.None);
        var igual = await casos.PreviaAsync(aluguel.Id, comando, versao, CancellationToken.None);
        Assert.AreEqual(previa.VersoesAceitas.ResumoCalculo, igual.VersoesAceitas.ResumoCalculo);
        await Unidade(cenario).ExecutarAsync(async token =>
        {
            var atual = await new RepositorioAlugueis(cenario.Banco).ConsultarAsync(aluguel.Id, token);
            await casos.RecalcularAceiteAsync(atual!, new(comando, previa.VersoesAceitas), token);
            return 0;
        }, CancellationToken.None);
        Assert.AreEqual(0, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(0, await cenario.Banco.ItensDevolucao.CountAsync());
        Assert.AreEqual(0, await cenario.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.ComandosIdempotentes.CountAsync());
        Assert.AreEqual(EstadoAluguel.EmAberto, (await cenario.Banco.Alugueis.AsNoTracking().SingleAsync()).Estado);
    }

    [TestMethod]
    public async Task VigenciaAusenteBloqueiaTanqueCheioMasZeroExplicitoNoRetornoEValido()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        var casos = Casos(cenario);
        var comando = new ComandoDevolucao(CenarioLocacoesSql.Agora.AddDays(2), 1000, 50m, []);
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.PreviaAsync(aluguel.Id, comando, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None));
        Assert.AreEqual("combustivel_sem_vigencia", erro.Codigo);
        await Unidade(cenario).ExecutarAsync(_ =>
        {
            cenario.Banco.Add(PrecoCombustivel.Novo(aluguel.EmpresaId, TipoCombustivel.Gasolina, 99m, comando.RetornoReal.AddTicks(1)));
            return Task.FromResult(0);
        }, CancellationToken.None);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.PreviaAsync(aluguel.Id, comando, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None));
        await Unidade(cenario).ExecutarAsync(_ =>
        {
            cenario.Banco.Add(PrecoCombustivel.Novo(aluguel.EmpresaId, TipoCombustivel.Gasolina, 0m, comando.RetornoReal));
            return Task.FromResult(0);
        }, CancellationToken.None);
        var previa = await casos.PreviaAsync(aluguel.Id, comando, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None);
        Assert.AreEqual(0m, previa.Calculo.Componentes.Single(c => c.Categoria == CategoriaComponenteFinal.Combustivel).ValorArredondado);
        Assert.AreEqual(0m, previa.Calculo.Entradas.Combustivel!.PrecoUnitario);
        // CNH válida na contratação (2028) não impede retorno real posterior ao vencimento.
        var tardia = await casos.PreviaAsync(aluguel.Id, comando with { RetornoReal = CenarioLocacoesSql.Agora.AddYears(3) },
            VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None);
        Assert.IsTrue(tardia.Calculo.EmAtraso);
        Assert.IsGreaterThan(2, tardia.Calculo.Diarias);
    }

    [TestMethod]
    public async Task RecalculoRejeitaNovaVigenciaRelevanteOuTaxaReajustadaEExcluida()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        var dados = await PrepararDadosAsync(cenario);
        var casos = Casos(cenario);
        var comando = Comando(dados);
        var previa = await casos.PreviaAsync(aluguel.Id, comando, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None);
        await Unidade(cenario).ExecutarAsync(_ =>
        {
            cenario.Banco.Add(PrecoCombustivel.Novo(aluguel.EmpresaId, TipoCombustivel.Gasolina, 8m, comando.RetornoReal));
            return Task.FromResult(0);
        }, CancellationToken.None);
        await ExigirConflitoAsync(cenario, casos, aluguel.Id, comando, previa.VersoesAceitas);
        previa = await casos.PreviaAsync(aluguel.Id, comando, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None);
        await Unidade(cenario).ExecutarAsync(async token =>
        {
            var taxa = await cenario.Banco.TaxasServicos.SingleAsync(t => t.Id == dados.TaxasAdicionais.Single().Id, token);
            taxa.Alterar("Taxa reajustada", 99m, ModalidadeTaxa.Diaria, CenarioLocacoesSql.Agora.AddDays(1));
            return 0;
        }, CancellationToken.None);
        await ExigirConflitoAsync(cenario, casos, aluguel.Id, comando, previa.VersoesAceitas);
        previa = await casos.PreviaAsync(aluguel.Id, comando, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None);
        await Unidade(cenario).ExecutarAsync(async token =>
        {
            var taxa = await cenario.Banco.TaxasServicos.SingleAsync(t => t.Id == dados.TaxasAdicionais.Single().Id, token);
            taxa.Excluir(CenarioLocacoesSql.Agora.AddDays(1));
            return 0;
        }, CancellationToken.None);
        await ExigirConflitoAsync(cenario, casos, aluguel.Id, comando, previa.VersoesAceitas);
        Assert.AreEqual(0, await cenario.Banco.DemonstrativosFinais.CountAsync());
    }

    [TestMethod]
    public async Task NegamAluguelTaxaExternaEExcluidaSemRevelarReferencia()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(a);
        var dados = await PrepararDadosAsync(a);
        var comando = Comando(dados);
        var versao = VersaoCadastro.Etag(aluguel.RowVersion);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => Casos(b).PreviaAsync(aluguel.Id, comando, versao, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => Casos(a).PreviaAsync(aluguel.Id,
            comando with { TaxasAdicionaisIds = [b.Dados.Taxas.Single().Id] }, versao, CancellationToken.None));
        await Unidade(a).ExecutarAsync(async token =>
        {
            var taxa = await a.Banco.TaxasServicos.SingleAsync(t => t.Id == dados.TaxasAdicionais.Single().Id, token);
            taxa.Excluir(CenarioLocacoesSql.Agora);
            return 0;
        }, CancellationToken.None);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            Casos(a).PreviaAsync(aluguel.Id, comando, versao, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            Casos(a).RecalcularAceiteAsync(aluguel, new(comando with { TaxasAdicionaisIds = [b.Dados.Taxas.Single().Id] },
                new(aluguel.RevisaoAtual.Id, "devolucao-v1", dados.Combustivel.Id, [], new string('A', 64))), CancellationToken.None));
    }

    internal static PreviaDevolucao Casos(CenarioLocacoesSql cenario)
        => new(new ConsultasDevolucao(cenario.Banco, new RepositorioPrecosCombustiveis(cenario.Banco), cenario.Contexto), cenario.Contexto);

    internal static ComandoDevolucao Comando(DadosDevolucao dados)
        => new(dados.RetornoReal, dados.HodometroFinal, dados.LitrosRestantes, dados.TaxasAdicionais.Select(t => t.Id).ToArray());

    private static async Task ExigirConflitoAsync(CenarioLocacoesSql cenario, PreviaDevolucao casos, Guid id,
        ComandoDevolucao comando, VersoesDevolucaoAceitas versoes)
    {
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => Unidade(cenario).ExecutarAsync(async token =>
        {
            var atual = await new RepositorioAlugueis(cenario.Banco).ConsultarAsync(id, token);
            return await casos.RecalcularAceiteAsync(atual!, new(comando, versoes), token);
        }, CancellationToken.None));
        Assert.AreEqual("previa_desatualizada", erro.Codigo);
    }
}
