using System.Globalization;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.Locacoes;

[TestClass]
public sealed class HashContratacaoTests
{
    [TestMethod]
    public void OrdenaArraysNormalizaInstanteENaoDependeDaCultura()
    {
        var comando = new EstimativasTests.Consultas().Comando with
        {
            TaxasServicoIds = [Guid.NewGuid(), Guid.NewGuid()],
            Seguros = [TipoSeguro.Cliente, TipoSeguro.Terceiros],
        };
        var primeiro = HashContratacao.Calcular(comando);
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var outro = comando with
            {
                TaxasServicoIds = comando.TaxasServicoIds.Reverse().ToArray(),
                Seguros = comando.Seguros.Reverse().ToArray(),
                RetornoPrevisto = comando.RetornoPrevisto.ToOffset(TimeSpan.FromHours(-3)),
            };
            Assert.AreEqual(primeiro, HashContratacao.Calcular(outro));
            Assert.AreEqual(64, primeiro.Length);
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }

    [TestMethod]
    public void TodaMudancaDeIntencaoAlteraHashEDuplicadosNaoSaoDeduplicados()
    {
        var comando = new EstimativasTests.Consultas().Comando;
        var hash = HashContratacao.Calcular(comando);
        foreach (var outro in new[]
        {
            comando with { ClienteId = Guid.NewGuid() }, comando with { CondutorId = Guid.NewGuid() },
            comando with { VeiculoId = Guid.NewGuid() }, comando with { PlanoId = Guid.NewGuid() },
            comando with { RetornoPrevisto = comando.RetornoPrevisto.AddTicks(1) },
            comando with { HodometroInicial = 1 }, comando with { TaxasServicoIds = [Guid.NewGuid()] },
            comando with { Seguros = [TipoSeguro.Cliente] },
        })
            Assert.AreNotEqual(hash, HashContratacao.Calcular(outro));
        var taxa = Guid.NewGuid();
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => HashContratacao.Calcular(comando with { TaxasServicoIds = [taxa, taxa] }));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => HashContratacao.Calcular(comando with { Seguros = [TipoSeguro.Cliente, TipoSeguro.Cliente] }));
    }
}
