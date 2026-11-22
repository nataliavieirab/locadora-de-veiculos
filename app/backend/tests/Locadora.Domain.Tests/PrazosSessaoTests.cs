using Locadora.Domain.Acesso;

namespace Locadora.Domain.Tests;

[TestClass]
public sealed class PrazosSessaoTests
{
    [TestMethod]
    public void AcessoDuraQuinzeMinutosQuandoASessaoAindaTemOitoHoras()
    {
        var agora = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var fim = agora.Add(PrazosSessao.DuracaoAbsoluta);

        var expira = PrazosSessao.ExpiracaoAcesso(agora, fim);

        Assert.AreEqual(agora.Add(PrazosSessao.AcessoMaximo), expira);
    }

    [TestMethod]
    public void AcessoTerminaComASessaoQuandoFaltamMenosDeQuinzeMinutos()
    {
        var agora = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var fim = agora.AddMinutes(5);

        var expira = PrazosSessao.ExpiracaoAcesso(agora, fim);

        Assert.AreEqual(fim, expira);
        Assert.AreNotEqual(PrazosSessao.ToleranciaRelogio, PrazosSessao.AcessoMaximo);
    }

    [TestMethod]
    public void DomainNaoReferenciaInfraestrutura()
    {
        var nomes = typeof(Empresa).Assembly.GetReferencedAssemblies().Select(referencia => referencia.Name!).ToArray();

        Assert.IsFalse(nomes.Any(nome => nome.Contains("EntityFramework", StringComparison.Ordinal)));
        Assert.IsFalse(nomes.Any(nome => nome.Contains("Identity", StringComparison.Ordinal)));
        Assert.IsFalse(nomes.Any(nome => nome.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)));
    }
}
