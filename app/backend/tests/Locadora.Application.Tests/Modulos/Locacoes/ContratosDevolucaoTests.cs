using Locadora.Application.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.Locacoes;

[TestClass]
public sealed class ContratosDevolucaoTests
{
    private static ComandoDevolucao Comando => new(DateTimeOffset.UnixEpoch, 0, 0m, []);

    [TestMethod]
    public void ComandoValidaFormaSemEscolherEmpresaOuAnteciparElegibilidadeDoContrato()
    {
        Comando.Validar();
        (Comando with { HodometroFinal = int.MaxValue, LitrosRestantes = 999999999.999m }).Validar();
        Assert.IsFalse(typeof(ComandoDevolucao).GetProperties().Any(p => p.Name is "EmpresaId" or "ContaId" or "TotalFinal"));
        Assert.IsFalse(typeof(ComandoConclusaoDevolucao).GetProperties().Any(p => p.Name is "EmpresaId" or "ContaId" or "TotalFinal"));
    }

    [TestMethod]
    public void NaoAceitaHodometroOuLitrosInvalidosNemTaxasRepetidas()
    {
        var taxa = Guid.NewGuid();
        foreach (var comando in new[]
        {
            Comando with { HodometroFinal = -1 }, Comando with { LitrosRestantes = -1m },
            Comando with { LitrosRestantes = 0.0001m }, Comando with { LitrosRestantes = 1000000000m },
            Comando with { TaxasAdicionaisIds = null! }, Comando with { TaxasAdicionaisIds = [Guid.Empty] },
            Comando with { TaxasAdicionaisIds = [taxa, taxa] },
        })
            Assert.ThrowsExactly<ValidacaoCadastroException>(comando.Validar);
    }

    [TestMethod]
    public void DomainEApplicationNaoReferenciamTransporteOuPersistencia()
    {
        foreach (var assembly in new[] { typeof(ComandoDevolucao).Assembly, typeof(ValidacaoCadastroException).Assembly })
        {
            var referencias = assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();
            Assert.IsFalse(referencias.Any(nome => nome.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || nome.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || nome.StartsWith("Locadora.Infrastructure", StringComparison.Ordinal)
                || nome.StartsWith("Locadora.Api", StringComparison.Ordinal)));
        }
    }
}
