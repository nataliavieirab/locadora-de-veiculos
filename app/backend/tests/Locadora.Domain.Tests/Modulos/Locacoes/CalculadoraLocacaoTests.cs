using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.Locacoes;

[TestClass]
public sealed class CalculadoraLocacaoTests
{
    private static readonly DateTimeOffset Saida = new(2026, 11, 23, 13, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlanoId = Guid.NewGuid();
    private static ParametrosPlano Diario => new(ModalidadePlano.Diario, 100, 2, null, null);
    private static ParametrosPlano Controlado => new(ModalidadePlano.Controlado, 100, null, 100, 2);
    private static ParametrosPlano Livre => new(ModalidadePlano.Livre, 100, null, null, null);
    private static AdicionalCalculo[] Adicionais =>
    [
        new(CategoriaItem.TaxaServico, Guid.NewGuid(), null, "Taxa fixa", ModalidadeTaxa.Fixa, 30),
        new(CategoriaItem.TaxaServico, Guid.NewGuid(), null, "Taxa diária", ModalidadeTaxa.Diaria, 10),
        new(CategoriaItem.Seguro, Guid.NewGuid(), TipoSeguro.Cliente, "Seguro cliente", ModalidadeTaxa.Diaria, 5),
        new(CategoriaItem.Seguro, Guid.NewGuid(), TipoSeguro.Terceiros, "Seguro terceiros", ModalidadeTaxa.Diaria, 7),
    ];

    [TestMethod]
    public void ReferenciasCa07ProduzemQuinhentosTrezentosEDuzentos()
    {
        Assert.AreEqual(500m, Calcular(Diario, 150).TotalConhecido);
        Assert.AreEqual(300m, Calcular(Controlado, 250).TotalConhecido);
        foreach (var km in new[] { 0, 150, 250, int.MaxValue })
            Assert.AreEqual(200m, Calcular(Livre, km).TotalConhecido);
        Assert.AreEqual(200m, Calcular(Controlado, 200).TotalConhecido);
        Assert.AreEqual(200m, Calcular(Controlado, 199).TotalConhecido);
    }

    [TestMethod]
    public void EstimativaMantemKAusenteEExplicaFranquiaEPendencias()
    {
        foreach (var plano in new[] { Diario, Controlado, Livre })
        {
            var resultado = Calcular(plano);
            Assert.IsNull(resultado.Quilometros);
            Assert.AreEqual(200m, resultado.TotalConhecido);
            Assert.AreEqual(plano.Modalidade != ModalidadePlano.Livre, resultado.QuilometragemPendente);
            Assert.AreEqual(plano.Modalidade == ModalidadePlano.Controlado ? 200L : (long?)null, resultado.FranquiaTotalKm);
            Assert.IsTrue(resultado.CombustivelPendente);
            Assert.IsTrue(resultado.AtrasoPendente);
            Assert.AreEqual(0m, resultado.TermoKmBruto);
        }
    }

    [TestMethod]
    public void AdicionaisSomamSetentaEQuatroECaucaoNaoIntegraDuzentosESetentaEQuatro()
    {
        var resultado = CalculadoraLocacao.Calcular(Saida, Saida.AddDays(2), PlanoId, Controlado, Adicionais);
        CollectionAssert.AreEqual(new[] { 200m, 30m, 20m, 10m, 14m },
            resultado.Componentes.Select(item => item.ValorArredondado).ToArray());
        Assert.AreEqual(74m, resultado.Componentes.Skip(1).Sum(item => item.ValorArredondado));
        Assert.AreEqual(274m, resultado.TotalConhecido);
        Assert.AreEqual(1000m, ResultadoCalculo.Caucao);
        Assert.AreEqual("contratacao-v1", ResultadoCalculo.VersaoRegra);
    }

    [TestMethod]
    public void ArredondaMeioParaCimaPorComponenteESomaOsArredondados()
    {
        var adicionais = Adicionais.Take(2).Select(a => a with { PrecoUnitario = 1.005m, Modalidade = ModalidadeTaxa.Fixa }).ToArray();
        var resultado = CalculadoraLocacao.Calcular(Saida, Saida.AddDays(1), PlanoId,
            Livre with { PrecoDiario = 1.005m }, adicionais);
        Assert.AreEqual(3.03m, resultado.TotalConhecido);
        Assert.IsTrue(resultado.Componentes.All(c => c.ValorBruto == 1.005m && c.ValorArredondado == 1.01m));
    }

    [TestMethod]
    public void BArredondaUmaVezSemArredondarDiariaEKmSeparadamente()
    {
        var resultado = CalculadoraLocacao.Calcular(Saida, Saida.AddDays(1), PlanoId,
            Diario with { PrecoDiario = 1.005m, PrecoKm = 1.005m }, [], 1);
        Assert.AreEqual(1.005m, resultado.TermoDiariasBruto);
        Assert.AreEqual(1.005m, resultado.TermoKmBruto);
        Assert.AreEqual(2.01m, resultado.TotalConhecido);
        Assert.HasCount(1, resultado.Componentes);
    }

    [TestMethod]
    public void NaoDuplicaTaxaNemTipoSeguroERecalculoRemoveOpcional()
    {
        var adicionais = Adicionais;
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            CalculadoraLocacao.Calcular(Saida, Saida.AddDays(2), PlanoId, Livre, [adicionais[0], adicionais[0]]));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            CalculadoraLocacao.Calcular(Saida, Saida.AddDays(2), PlanoId, Livre,
                [adicionais[2], adicionais[2] with { OrigemId = Guid.NewGuid() }]));
        var completo = CalculadoraLocacao.Calcular(Saida, Saida.AddDays(2), PlanoId, Livre, adicionais);
        var removido = CalculadoraLocacao.Calcular(Saida, Saida.AddDays(2), PlanoId, Livre, adicionais[..3]);
        Assert.AreEqual(274m, completo.TotalConhecido);
        Assert.AreEqual(260m, removido.TotalConhecido);
        Assert.HasCount(5, completo.Componentes);
        Assert.HasCount(4, removido.Componentes);
    }

    [TestMethod]
    public void RejeitaEscalaIntervaloEParametrosIncompativeisSemTruncar()
    {
        foreach (var plano in new[]
        {
            Livre with { PrecoDiario = -1 }, Livre with { PrecoDiario = 0.0000001m },
            Livre with { PrecoDiario = 10000000000000m }, Livre with { PrecoKm = 0 },
            Diario with { PrecoKm = null }, Diario with { FranquiaDiariaKm = 0 },
            Diario with { PrecoKm = -1 }, Diario with { PrecoKm = 0.0000001m },
            Controlado with { FranquiaDiariaKm = -1 }, Controlado with { PrecoKmExcedente = null },
            Controlado with { PrecoKm = 1 }, Controlado with { PrecoKmExcedente = -1 },
            Livre with { Modalidade = (ModalidadePlano)99 },
        })
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => Calcular(plano));
        foreach (var valor in new[] { -1m, 0.0000001m, 10000000000000m })
            Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
                CalculadoraLocacao.Calcular(Saida, Saida.AddDays(2), PlanoId, Livre,
                    [Adicionais[0] with { PrecoUnitario = valor }]));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => CalculadoraLocacao.ArredondarComponente(0.0000000001m));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Calcular(Diario, -1));
    }

    [TestMethod]
    public void LimiteDeSaidaETotalSaoValidadosAntesDaPersistencia()
    {
        Assert.AreEqual(CalculadoraLocacao.MaximoMonetario,
            CalculadoraLocacao.ArredondarComponente(CalculadoraLocacao.MaximoMonetario));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            CalculadoraLocacao.ArredondarComponente(CalculadoraLocacao.MaximoMonetario + 0.01m));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            CalculadoraLocacao.Calcular(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, PlanoId,
                Livre with { PrecoDiario = 9999999999999.999999m }, []));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Calcular(Diario with { PrecoKm = 9999999999999.999999m }, int.MaxValue));
        // Componentes isoladamente caberiam; a soma não cabe em decimal(19,2).
        var inicio = DateTimeOffset.MinValue;
        var fim = inicio.AddDays(6000);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            CalculadoraLocacao.Calcular(inicio, fim, PlanoId, Livre with { PrecoDiario = 9999999999999m },
                [Adicionais[1] with { PrecoUnitario = 9999999999999m }]));
        Assert.AreEqual(0m, Calcular(Livre with { PrecoDiario = 0 }).TotalConhecido);
    }

    [TestMethod]
    public void QuilometrosSaoInteirosNaoNegativosESemDuplaCobrancaNoLivre()
    {
        Assert.AreEqual(150, CalculadoraLocacao.QuilometrosRodados(1000, 1150));
        Assert.AreEqual(int.MaxValue, CalculadoraLocacao.QuilometrosRodados(0, int.MaxValue));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => CalculadoraLocacao.QuilometrosRodados(1000, 999));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => CalculadoraLocacao.QuilometrosRodados(-1, 1));
        var resultado = Calcular(Livre, 150);
        Assert.IsFalse(resultado.QuilometragemPendente);
        Assert.AreEqual(0m, resultado.TermoKmBruto);
        Assert.HasCount(1, resultado.Componentes);
    }

    private static ResultadoCalculo Calcular(ParametrosPlano plano, int? km = null)
        => CalculadoraLocacao.Calcular(Saida, Saida.AddDays(2), PlanoId, plano, [], km);
}
