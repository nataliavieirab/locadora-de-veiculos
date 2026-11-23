using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.FrotaPrecos;

[TestClass]
public sealed class TaxasSegurosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(ModalidadeTaxa.Fixa)]
    [DataRow(ModalidadeTaxa.Diaria)]
    public void TaxaPodeSerCriadaEditadaEExcluida(ModalidadeTaxa modalidade)
    {
        var taxa = TaxaServico.Nova(Guid.NewGuid(), "Assento sintético", 18m, modalidade, Agora);
        var capturado = taxa.Preco;
        var id = taxa.Id;
        taxa.Alterar("Novo nome", 20.123456m, modalidade, Agora.AddMinutes(1));
        Assert.AreEqual("Novo nome", taxa.Nome);
        Assert.AreEqual(20.123456m, taxa.Preco);
        Assert.AreEqual(modalidade, taxa.Modalidade);
        Assert.AreEqual(18m, capturado);
        taxa.Excluir(Agora.AddMinutes(2));
        Assert.AreEqual(id, taxa.Id);
        Assert.IsNotNull(taxa.ExcluidoEmUtc);
        Assert.ThrowsExactly<InvalidOperationException>(() => taxa.Alterar("Outro", 0m, modalidade, Agora.AddMinutes(3)));
    }

    [TestMethod]
    public void TaxaRejeitaNomePrecoOuModalidadeInvalidosSemAlteracaoParcial()
    {
        var taxa = TaxaServico.Nova(Guid.NewGuid(), new string('A', 200), 0m, ModalidadeTaxa.Fixa, Agora);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => taxa.Alterar("", 0m, ModalidadeTaxa.Fixa, Agora));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => taxa.Alterar(new string('B', 201), 0m, ModalidadeTaxa.Fixa, Agora));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => taxa.Alterar("Outro", 0m, (ModalidadeTaxa)99, Agora));
        foreach (var preco in new[] { -1m, 0.0000001m, 1.0000000m, 10000000000000m })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => taxa.Alterar("Outro", preco, ModalidadeTaxa.Diaria, Agora.AddMinutes(1)));
        }

        Assert.AreEqual(new string('A', 200), taxa.Nome);
        Assert.AreEqual(0m, taxa.Preco);
        Assert.AreEqual(ModalidadeTaxa.Fixa, taxa.Modalidade);
        Assert.AreEqual(Agora, taxa.AlteradoEmUtc);
    }

    [TestMethod]
    public void PrecosDeSegurosSaoIndependentesEZeroDeveSerExplicito()
    {
        var empresa = Guid.NewGuid();
        var cliente = PrecoSeguro.Novo(empresa, TipoSeguro.Cliente, 0m, Agora);
        var terceiros = PrecoSeguro.Novo(empresa, TipoSeguro.Terceiros, 7m, Agora);
        var capturado = terceiros.PrecoDiario;
        terceiros.Alterar(10m, Agora.AddMinutes(1));

        Assert.AreEqual(0m, cliente.PrecoDiario);
        Assert.AreEqual(10m, terceiros.PrecoDiario);
        Assert.AreEqual(7m, capturado);
        Assert.AreNotEqual(cliente.Id, terceiros.Id);
        Assert.AreEqual(TipoSeguro.Cliente, cliente.Tipo);
        Assert.AreEqual(TipoSeguro.Terceiros, terceiros.Tipo);
    }

    [TestMethod]
    public void SeguroRejeitaPrecoOuTipoInvalidos()
    {
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => PrecoSeguro.Novo(Guid.NewGuid(), (TipoSeguro)99, 0m, Agora));
        var seguro = PrecoSeguro.Novo(Guid.NewGuid(), TipoSeguro.Cliente, 9999999999999.999999m, Agora);
        foreach (var preco in new[] { -1m, 0.0000001m, 1.0000000m, 10000000000000m })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => seguro.Alterar(preco, Agora.AddMinutes(1)));
        }

        Assert.AreEqual(9999999999999.999999m, seguro.PrecoDiario);
        Assert.AreEqual(Agora, seguro.AlteradoEmUtc);
    }
}
