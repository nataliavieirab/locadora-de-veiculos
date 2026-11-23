using Locadora.Application.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.Transversal;

[TestClass]
public sealed class VersaoCadastroTests
{
    private static readonly byte[] Versao = [0, 0, 0, 0, 0, 0, 0, 1];

    [TestMethod]
    public void EtagEOpacoForteEExigeVersaoExata()
    {
        var tag = VersaoCadastro.Etag(Versao);
        Assert.AreEqual("\"AAAAAAAAAAE=\"", tag);
        VersaoCadastro.Exigir(tag, Versao);
        VersaoCadastro.Exigir(" " + tag + " ", Versao);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void AusenciaExigePrecondicao(string? valor)
    {
        var erro = Assert.ThrowsExactly<PrecondicaoCadastroException>(() => VersaoCadastro.Exigir(valor, Versao));
        Assert.IsTrue(erro.Ausente);
    }

    [TestMethod]
    [DataRow("*")]
    [DataRow("W/\"AAAAAAAAAAE=\"")]
    [DataRow("\"AAAAAAAAAAI=\"")]
    [DataRow("AAAAAAAAAAE=")]
    public void VersaoDiferenteFracaOuCuringaNaoPermiteSobrescrita(string valor)
    {
        var erro = Assert.ThrowsExactly<PrecondicaoCadastroException>(() => VersaoCadastro.Exigir(valor, Versao));
        Assert.IsFalse(erro.Ausente);
    }

    [TestMethod]
    public void VersaoNaoPersistidaNaoProduzEtag()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => VersaoCadastro.Etag([]));
    }
}
