using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.Locacoes;

[TestClass]
public sealed class CalculadoraDevolucaoTests
{
    private static readonly DateTimeOffset Saida = new(2026, 11, 22, 13, 0, 0, TimeSpan.Zero);
    private static readonly ParametrosPlano Livre = new(ModalidadePlano.Livre, 100m, null, null, null);
    private static EntradaCalculoFinal Entrada => new(Saida, Saida.AddDays(2), Saida.AddDays(2),
        18420, 18570, Guid.NewGuid(), Livre, TipoCombustivel.Gasolina, 50m, 40m,
        new(Guid.NewGuid(), TipoCombustivel.Gasolina, Saida.AddDays(-1), 6m),
        [
            Taxa("Higienização", ModalidadeTaxa.Fixa, 30m, OrigemInclusao.Contratacao),
            Taxa("Apoio de viagem", ModalidadeTaxa.Diaria, 10m, OrigemInclusao.Devolucao),
            new(new(CategoriaItem.Seguro, Guid.NewGuid(), TipoSeguro.Cliente,
                "Seguro cliente", ModalidadeTaxa.Diaria, 5m), OrigemInclusao.Contratacao),
            new(new(CategoriaItem.Seguro, Guid.NewGuid(), TipoSeguro.Terceiros,
                "Seguro terceiros", ModalidadeTaxa.Diaria, 7m), OrigemInclusao.Edicao),
        ]);

    [TestMethod]
    [DataRow(0L, 1)]
    [DataRow(TimeSpan.TicksPerDay, 1)]
    [DataRow(TimeSpan.TicksPerDay + TimeSpan.TicksPerMinute, 2)]
    [DataRow(TimeSpan.TicksPerDay + 1, 2)]
    public void Rn01Ca07ContaDuracaoRealInclusiveZeroELimiteDeUmTick(long ticks, int diarias)
    {
        var resultado = CalculadoraDevolucao.Calcular(Entrada with { RetornoReal = Saida.AddTicks(ticks) });
        Assert.AreEqual(diarias, resultado.Diarias);
        Assert.AreEqual(diarias * 100m, resultado.TermoDiariasBruto);
    }

    [TestMethod]
    public void Rn01UsaInstantesUtcInclusiveTransicoesHistoricas()
    {
        foreach (var (inicio, fim, diarias) in new[]
        {
            ("2018-11-03T12:00:00-03:00", "2018-11-04T12:00:00-02:00", 1),
            ("2019-02-16T12:00:00-02:00", "2019-02-17T12:00:00-03:00", 2),
        })
        {
            var saida = DateTimeOffset.Parse(inicio);
            var retorno = DateTimeOffset.Parse(fim);
            var entrada = Entrada;
            var resultado = CalculadoraDevolucao.Calcular(entrada with
            {
                Saida = saida, RetornoPrevisto = retorno, RetornoReal = retorno,
                Combustivel = entrada.Combustivel! with { InicioVigenciaUtc = saida },
            });
            Assert.AreEqual(diarias, resultado.Diarias);
            Assert.AreEqual(TimeSpan.Zero, resultado.Entradas.Saida.Offset);
            Assert.AreEqual(TimeSpan.Zero, resultado.Entradas.RetornoReal.Offset);
        }
        var baseEntrada = Entrada;
        var equivalente = CalculadoraDevolucao.Calcular(baseEntrada with
        {
            RetornoReal = baseEntrada.RetornoReal.ToOffset(TimeSpan.FromHours(9)),
        });
        Assert.AreEqual(2, equivalente.Diarias);
        Assert.IsFalse(equivalente.EmAtraso);
        Assert.AreEqual(334m, equivalente.TotalFinal);
    }

    [TestMethod]
    public void Rn02A05Ca07TresPlanosPreservamTermosEFranquia()
    {
        var entrada = Entrada with { Adicionais = [], LitrosRestantes = 50m };
        var diario = CalculadoraDevolucao.Calcular(entrada with
        {
            Plano = new(ModalidadePlano.Diario, 100m, 2m, null, null),
        });
        Assert.AreEqual(150, diario.Quilometros);
        Assert.AreEqual(200m, diario.TermoDiariasBruto);
        Assert.AreEqual(300m, diario.TermoKmBruto);
        Assert.AreEqual(500m, diario.TotalFinal);
        Assert.IsNull(diario.FranquiaTotalKm);
        Assert.IsNull(diario.ExcedenteKm);
        var controlado = CalculadoraDevolucao.Calcular(entrada with
        {
            Plano = new(ModalidadePlano.Controlado, 100m, null, 100, 2m), HodometroFinal = 18670,
        });
        Assert.AreEqual(250, controlado.Quilometros);
        Assert.AreEqual(200L, controlado.FranquiaTotalKm);
        Assert.AreEqual(50L, controlado.ExcedenteKm);
        Assert.AreEqual(100m, controlado.TermoKmBruto);
        Assert.AreEqual(300m, controlado.TotalFinal);
        foreach (var km in new[] { 0, 150, 250, int.MaxValue })
        {
            var livre = CalculadoraDevolucao.Calcular(entrada with { HodometroInicial = 0, HodometroFinal = km });
            Assert.AreEqual(km, livre.Quilometros);
            Assert.AreEqual(0m, livre.TermoKmBruto);
            Assert.AreEqual(200m, livre.TotalFinal);
        }
        foreach (var km in new[] { 0, 199, 200 })
        {
            var abaixoDaFranquia = CalculadoraDevolucao.Calcular(entrada with
            {
                Plano = new(ModalidadePlano.Controlado, 100m, null, 100, 2m),
                HodometroInicial = 0, HodometroFinal = km,
            });
            Assert.AreEqual(0L, abaixoDaFranquia.ExcedenteKm);
            Assert.AreEqual(200m, abaixoDaFranquia.TotalFinal);
        }
    }

    [TestMethod]
    public void Rn06A09Ca08Somam334Ou354ComCaucaoSeparadaEAtrasoSemNovaDiaria()
    {
        var entrada = Entrada;
        var semAtraso = CalculadoraDevolucao.Calcular(entrada);
        var comAtraso = CalculadoraDevolucao.Calcular(entrada with { RetornoPrevisto = entrada.RetornoReal.AddMinutes(-1) });
        CollectionAssert.AreEqual(new[] { 200m, 30m, 20m, 10m, 14m, 60m, 0m },
            semAtraso.Componentes.Select(c => c.ValorArredondado).ToArray());
        Assert.AreEqual(334m, semAtraso.TotalFinal);
        Assert.AreEqual(354m, comAtraso.TotalFinal);
        Assert.AreEqual(semAtraso.Diarias, comAtraso.Diarias);
        Assert.AreEqual(20m, comAtraso.Componentes[^1].ValorArredondado);
        Assert.IsTrue(comAtraso.EmAtraso);
        Assert.IsFalse(semAtraso.EmAtraso);
        Assert.AreEqual(1000m, ResultadoCalculoFinal.Caucao);
        Assert.AreEqual("devolucao-v1", ResultadoCalculoFinal.VersaoRegra);
        Assert.AreEqual(comAtraso.TotalFinal, comAtraso.Componentes.Sum(c => c.ValorArredondado));
    }

    [TestMethod]
    public void Rn06Ca12TaxaNovaUsaTodasAsDiariasEIdentificaOrigemSemReescreverItens()
    {
        var entrada = Entrada;
        var resultado = CalculadoraDevolucao.Calcular(entrada);
        var taxa = resultado.Componentes.Single(c => c.OrigemInclusao == OrigemInclusao.Devolucao);
        Assert.AreEqual(CategoriaComponenteFinal.TaxaServico, taxa.Categoria);
        Assert.AreEqual(ModalidadeTaxa.Diaria, taxa.Modalidade);
        Assert.AreEqual(2m, taxa.Quantidade);
        Assert.AreEqual(10m, taxa.PrecoUnitario);
        Assert.AreEqual(20m, taxa.ValorBruto);
        Assert.AreEqual(20m, taxa.ValorArredondado);
        CollectionAssert.AreEqual(entrada.Adicionais.ToArray(), resultado.Entradas.Adicionais.ToArray());
        Assert.AreEqual(OrigemInclusao.Edicao, resultado.Componentes[4].OrigemInclusao);
    }

    [TestMethod]
    public void Rn07UsaLitrosFracionariosEConservaNoveCasasBrutasSemTruncar()
    {
        var entrada = Entrada;
        var resultado = CalculadoraDevolucao.Calcular(entrada with
        {
            CapacidadeLitros = 50.123m, LitrosRestantes = 40m,
            Combustivel = entrada.Combustivel! with { PrecoUnitario = 6.123456m },
        });
        var combustivel = resultado.Componentes.Single(c => c.Categoria == CategoriaComponenteFinal.Combustivel);
        Assert.AreEqual(10.123m, resultado.LitrosRepostos);
        Assert.AreEqual(10.123m, combustivel.Quantidade);
        Assert.AreEqual(6.123456m, combustivel.PrecoUnitario);
        Assert.AreEqual(61.987745088m, combustivel.ValorBruto);
        Assert.AreEqual(61.99m, combustivel.ValorArredondado);
        Assert.AreEqual(entrada.Combustivel!.Id, combustivel.OrigemId);
        Assert.AreEqual(entrada.Combustivel.InicioVigenciaUtc, resultado.Entradas.Combustivel!.InicioVigenciaUtc);
    }

    [TestMethod]
    public void Rn07TanqueCheioEPrecoZeroSaoExplicitosMasAusenciaOuVigenciaFuturaNaoValem()
    {
        var entrada = Entrada;
        foreach (var restantes in new[] { 40m, 50m })
        {
            foreach (var preco in new[] { 0m, 6m })
            {
                var resultado = CalculadoraDevolucao.Calcular(entrada with
                {
                    LitrosRestantes = restantes,
                    Combustivel = entrada.Combustivel! with { PrecoUnitario = preco, InicioVigenciaUtc = entrada.RetornoReal },
                });
                Assert.AreEqual((50m - restantes) * preco, resultado.Componentes[^2].ValorArredondado);
            }
            Rejeita(entrada with { LitrosRestantes = restantes, Combustivel = null }, "combustivel");
            Rejeita(entrada with
            {
                LitrosRestantes = restantes,
                Combustivel = entrada.Combustivel! with { InicioVigenciaUtc = entrada.RetornoReal.AddTicks(1) },
            }, "combustivel");
        }
    }

    [TestMethod]
    public void Rn08AtrasoDeUmTickCobraUmaMultaENaoIncideSobreDemaisComponentes()
    {
        var entrada = Entrada;
        var resultado = CalculadoraDevolucao.Calcular(entrada with
        {
            RetornoPrevisto = entrada.RetornoReal.AddTicks(-1),
            Adicionais = [Taxa("Taxa elevada", ModalidadeTaxa.Fixa, 10000m, OrigemInclusao.Devolucao)],
        });
        Assert.IsTrue(resultado.EmAtraso);
        Assert.AreEqual(2, resultado.Diarias);
        Assert.AreEqual(20m, resultado.Componentes[^1].ValorArredondado);
        Assert.HasCount(1, resultado.Componentes.Where(c => c.Categoria == CategoriaComponenteFinal.Multa));
        Assert.AreEqual(10280m, resultado.TotalFinal);
        var antecipado = CalculadoraDevolucao.Calcular(entrada with { RetornoReal = entrada.RetornoPrevisto.AddTicks(-1) });
        Assert.IsFalse(antecipado.EmAtraso);
        Assert.AreEqual(0m, antecipado.Componentes[^1].ValorArredondado);
    }

    [TestMethod]
    public void Rn09SomaTermosDeBAntesDeArredondarEMultaUsaBArredondado()
    {
        var entrada = Entrada;
        var resultado = CalculadoraDevolucao.Calcular(entrada with
        {
            RetornoReal = Saida.AddDays(1), RetornoPrevisto = Saida.AddDays(1).AddTicks(-1),
            HodometroFinal = entrada.HodometroInicial + 1,
            Plano = new(ModalidadePlano.Diario, 1.005m, 1.04m, null, null),
            Adicionais = [], LitrosRestantes = 50m,
        });
        Assert.AreEqual(1.005m, resultado.TermoDiariasBruto);
        Assert.AreEqual(1.04m, resultado.TermoKmBruto);
        Assert.AreEqual(2.045m, resultado.Componentes[0].ValorBruto);
        Assert.AreEqual(2.05m, resultado.Componentes[0].ValorArredondado);
        Assert.AreEqual(0.205m, resultado.Componentes[^1].ValorBruto);
        Assert.AreEqual(0.21m, resultado.Componentes[^1].ValorArredondado);
        Assert.AreEqual(2.26m, resultado.TotalFinal);
        // Arredondar os dois termos separados daria 2,06; usar B bruto na multa daria 0,20.
    }

    [TestMethod]
    public void Rn09CadaComponenteArredondaMeioParaCimaETotalNaoArredondaBrutosAgrupados()
    {
        var entrada = Entrada;
        var resultado = CalculadoraDevolucao.Calcular(entrada with
        {
            RetornoReal = Saida.AddDays(1), Plano = Livre with { PrecoDiario = 1.005m },
            Adicionais = [Taxa("Taxa", ModalidadeTaxa.Fixa, 1.005m, OrigemInclusao.Devolucao)],
            LitrosRestantes = 49m, Combustivel = entrada.Combustivel! with { PrecoUnitario = 1.005m },
        });
        CollectionAssert.AreEqual(new[] { 1.01m, 1.01m, 1.01m, 0m },
            resultado.Componentes.Select(c => c.ValorArredondado).ToArray());
        Assert.AreEqual(3.03m, resultado.TotalFinal);
    }

    [TestMethod]
    public void Ca09RejeitaRetornosInvalidosEHodometrosNegativosOuMenores()
    {
        var entrada = Entrada;
        Rejeita(entrada with { RetornoReal = Saida.AddTicks(-1) }, "retornoReal");
        Rejeita(entrada with { RetornoPrevisto = Saida }, "retornoPrevisto");
        Rejeita(entrada with { RetornoPrevisto = Saida.AddTicks(-1) }, "retornoPrevisto");
        Rejeita(entrada with { HodometroInicial = -1 }, "hodometroInicial");
        Rejeita(entrada with { HodometroFinal = -1 }, "hodometroFinal");
        Rejeita(entrada with { HodometroFinal = entrada.HodometroInicial - 1 }, "hodometroFinal");
        // A entrada é int: fracionários não são representáveis; rejeição JSON pertence aos testes HTTP.
        var limite = CalculadoraDevolucao.Calcular(entrada with { HodometroInicial = 0, HodometroFinal = int.MaxValue });
        Assert.AreEqual(int.MaxValue, limite.Quilometros);
    }

    [TestMethod]
    public void Ca09RejeitaLitrosCapacidadeEEscalaSemAjustarSilenciosamente()
    {
        var entrada = Entrada;
        foreach (var capacidade in new[] { -1m, 0m, 50.0001m, 1000000000m, decimal.MaxValue })
            Rejeita(entrada with { CapacidadeLitros = capacidade }, "capacidadeLitros");
        foreach (var litros in new[] { -0.001m, 50.001m, 0.0001m, 40.0000m, decimal.MaxValue })
            Rejeita(entrada with { LitrosRestantes = litros }, "litrosRestantes");
        var minimo = CalculadoraDevolucao.Calcular(entrada with { CapacidadeLitros = 0.001m, LitrosRestantes = 0m });
        Assert.AreEqual(0.001m, minimo.LitrosRepostos);
        var maximo = CalculadoraDevolucao.Calcular(entrada with { CapacidadeLitros = 999999999.999m, LitrosRestantes = 0m });
        Assert.AreEqual(999999999.999m, maximo.LitrosRepostos);
    }

    [TestMethod]
    public void RejeitaPrecoTipoEReferenciaDeCombustivelInvalidos()
    {
        var entrada = Entrada;
        foreach (var preco in new[] { -1m, 0.0000001m, 10000000000000m, decimal.MaxValue })
            Rejeita(entrada with { Combustivel = entrada.Combustivel! with { PrecoUnitario = preco } }, "combustivel");
        Rejeita(entrada with { Combustivel = entrada.Combustivel! with { Id = Guid.Empty } }, "combustivel");
        Rejeita(entrada with { Combustivel = entrada.Combustivel! with { Tipo = TipoCombustivel.Diesel } }, "combustivel");
        Rejeita(entrada with { TipoCombustivel = (TipoCombustivel)99 }, "combustivel");
        Rejeita(entrada with { Combustivel = entrada.Combustivel! with { Tipo = (TipoCombustivel)99 } }, "combustivel");
    }

    [TestMethod]
    public void Rn06RejeitaTaxaRepetidaEntreOrigensESeguroIncluidoNaDevolucao()
    {
        var entrada = Entrada;
        var taxa = entrada.Adicionais.First();
        Rejeita(entrada with { Adicionais = [taxa, taxa with { OrigemInclusao = OrigemInclusao.Devolucao }] }, "itens");
        var seguro = entrada.Adicionais.First(a => a.Item.Categoria == CategoriaItem.Seguro);
        Rejeita(entrada with { Adicionais = [seguro with { OrigemInclusao = OrigemInclusao.Devolucao }] }, "itens");
        Rejeita(entrada with
        {
            Adicionais = [seguro, seguro with { Item = seguro.Item with { OrigemId = Guid.NewGuid() } }],
        }, "itens");
        Rejeita(entrada with { Adicionais = [taxa with { OrigemInclusao = (OrigemInclusao)99 }] }, "itens");
        foreach (var preco in new[] { -1m, 0.0000001m, 10000000000000m })
            Rejeita(entrada with { Adicionais = [taxa with { Item = taxa.Item with { PrecoUnitario = preco } }] }, "itens");
    }

    [TestMethod]
    public void ValidaParametrosDoPlanoAntesDeCalcularESemRegressaoDaEstimativa()
    {
        var entrada = Entrada;
        foreach (var plano in new[]
        {
            Livre with { PrecoDiario = -1m }, Livre with { PrecoDiario = 0.0000001m },
            Livre with { PrecoDiario = 10000000000000m }, Livre with { PrecoKm = 1m },
            Livre with { Modalidade = (ModalidadePlano)99 },
        })
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => CalculadoraDevolucao.Calcular(entrada with { Plano = plano }));
        Rejeita(entrada with { PlanoId = Guid.Empty }, "planoId");
        var estimativa = CalculadoraLocacao.Calcular(Saida, entrada.RetornoPrevisto, entrada.PlanoId,
            new(ModalidadePlano.Diario, 100m, 2m, null, null), []);
        Assert.IsNull(estimativa.Quilometros);
        Assert.IsTrue(estimativa.QuilometragemPendente);
        Assert.IsTrue(estimativa.CombustivelPendente);
        Assert.IsTrue(estimativa.AtrasoPendente);
        Assert.AreEqual("contratacao-v1", ResultadoCalculo.VersaoRegra);
    }

    [TestMethod]
    public void RejeitaOverflowDeComponentesEDaSomaMesmoQuandoParcelasCabem()
    {
        var entrada = Entrada;
        Rejeita(entrada with
        {
            CapacidadeLitros = 999999999.999m, LitrosRestantes = 0m,
            Combustivel = entrada.Combustivel! with { PrecoUnitario = 9999999999999.999999m },
        }, "devolucao");
        // Plano e combustível cabem isolados, mas sua soma ultrapassa decimal(19,2).
        Rejeita(entrada with
        {
            RetornoPrevisto = Saida.AddDays(6000), RetornoReal = Saida.AddDays(6000),
            Plano = Livre with { PrecoDiario = 9999999999999m }, Adicionais = [],
            CapacidadeLitros = 6000m, LitrosRestantes = 0m,
            Combustivel = entrada.Combustivel! with { PrecoUnitario = 9999999999999m },
        }, "devolucao");
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => CalculadoraDevolucao.Calcular(entrada with
        {
            Plano = new(ModalidadePlano.Diario, 100m, 9999999999999.999999m, null, null),
            HodometroInicial = 0, HodometroFinal = int.MaxValue,
        }));
        var zerado = CalculadoraDevolucao.Calcular(entrada with
        {
            Plano = Livre with { PrecoDiario = 0m }, Adicionais = [],
            Combustivel = entrada.Combustivel! with { PrecoUnitario = 0m },
        });
        Assert.AreEqual(0m, zerado.TotalFinal);
        Assert.HasCount(3, zerado.Componentes);
    }

    [TestMethod]
    public void AceitaLimiteMonetarioExatoMasRejeitaUmCentavoAdicional()
    {
        var entrada = Entrada with
        {
            RetornoPrevisto = Saida.AddDays(10000), RetornoReal = Saida.AddDays(10000),
            Plano = Livre with { PrecoDiario = 9999999999999.999999m },
            LitrosRestantes = 50m, Adicionais = [],
        };
        var resultado = CalculadoraDevolucao.Calcular(entrada);
        Assert.AreEqual(CalculadoraLocacao.MaximoMonetario, resultado.TotalFinal);
        Assert.AreEqual(10000m, resultado.Componentes[0].Quantidade);
        Rejeita(entrada with
        {
            Adicionais = [Taxa("Um centavo", ModalidadeTaxa.Fixa, 0.01m, OrigemInclusao.Devolucao)],
        }, "devolucao");
        Rejeita(entrada with { RetornoPrevisto = entrada.RetornoReal.AddTicks(-1) }, "devolucao");
    }

    [TestMethod]
    public void FranquiaUsaLongEDuracaoNaoInventaLimiteFuturoDoRetorno()
    {
        var entrada = Entrada;
        var resultado = CalculadoraDevolucao.Calcular(entrada with
        {
            Saida = DateTimeOffset.MinValue, RetornoPrevisto = DateTimeOffset.MinValue.AddDays(1),
            RetornoReal = DateTimeOffset.MaxValue, HodometroInicial = 0, HodometroFinal = int.MaxValue,
            Plano = new(ModalidadePlano.Controlado, 0m, null, int.MaxValue, 1m),
            Adicionais = [], LitrosRestantes = 50m,
            Combustivel = entrada.Combustivel! with { InicioVigenciaUtc = DateTimeOffset.MinValue },
        });
        Assert.IsGreaterThan(1, resultado.Diarias);
        Assert.AreEqual((long)resultado.Diarias * int.MaxValue, resultado.FranquiaTotalKm);
        Assert.AreEqual(0L, resultado.ExcedenteKm);
        Assert.AreEqual(0m, resultado.TotalFinal);
    }

    [TestMethod]
    public void ResultadoNaoCompartilhaColecaoMutavelENaoDependeDeRelogioOuCadastro()
    {
        var entrada = Entrada;
        var adicionais = entrada.Adicionais.ToList();
        var resultado = CalculadoraDevolucao.Calcular(entrada with { Adicionais = adicionais });
        adicionais.Clear();
        Assert.HasCount(4, resultado.Entradas.Adicionais);
        Assert.HasCount(7, resultado.Componentes);
        Assert.AreEqual(334m, resultado.TotalFinal);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((ICollection<AdicionalCalculoFinal>)resultado.Entradas.Adicionais).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((ICollection<ComponenteFinalCalculado>)resultado.Componentes).Clear());
        var recomposicao = CalculadoraDevolucao.Calcular(resultado.Entradas);
        CollectionAssert.AreEqual(resultado.Componentes.ToArray(), recomposicao.Componentes.ToArray());
        Assert.AreEqual(resultado.TotalFinal, recomposicao.TotalFinal);
    }

    private static AdicionalCalculoFinal Taxa(string nome, ModalidadeTaxa modalidade, decimal preco, OrigemInclusao origem)
        => new(new(CategoriaItem.TaxaServico, Guid.NewGuid(), null, nome, modalidade, preco), origem);

    private static void Rejeita(EntradaCalculoFinal entrada, string campo)
    {
        var erro = Assert.ThrowsExactly<ValidacaoCadastroException>(() => CalculadoraDevolucao.Calcular(entrada));
        Assert.AreEqual(campo, erro.Campo);
    }
}
