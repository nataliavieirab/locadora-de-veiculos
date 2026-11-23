using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.FrotaPrecos;

[TestClass]
public sealed class GruposPlanosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void GrupoValidoPodeSerCriadoEditadoEExcluido()
    {
        var grupo = Grupo.Novo(Guid.NewGuid(), "Compactos", Agora);
        var id = grupo.Id;
        grupo.Alterar("Utilitários", Agora.AddMinutes(1));

        Assert.AreEqual("Utilitários", grupo.Nome);
        Assert.AreEqual(id, grupo.Id);

        grupo.Excluir(Agora.AddMinutes(2));
        Assert.IsNotNull(grupo.ExcluidoEmUtc);
        Assert.ThrowsExactly<InvalidOperationException>(() => grupo.Alterar("Outro", Agora.AddMinutes(3)));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void GrupoRejeitaNomeAusente(string? nome)
    {
        var erro = Assert.ThrowsExactly<ValidacaoCadastroException>(() => Grupo.Novo(Guid.NewGuid(), nome!, Agora));
        Assert.AreEqual("nome", erro.Campo);
    }

    [TestMethod]
    public void NomeNoLimiteNaoETruncadoEAcimaDoLimiteERejeitado()
    {
        var nome = new string('A', 200);
        var grupo = Grupo.Novo(Guid.NewGuid(), nome, Agora);
        Assert.AreEqual(nome, grupo.Nome);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => grupo.Alterar(nome + "B", Agora));
        Assert.AreEqual(nome, grupo.Nome);
    }

    [TestMethod]
    public void DiarioExigeSomenteDiariaEPrecoKm()
    {
        var plano = Novo(ModalidadePlano.Diario, precoKm: 1.25m);
        Assert.AreEqual(ModalidadePlano.Diario, plano.Modalidade);
        Assert.AreEqual(100m, plano.PrecoDiario);
        Assert.AreEqual(1.25m, plano.PrecoKm);
        Assert.IsNull(plano.FranquiaDiariaKm);
        Assert.IsNull(plano.PrecoKmExcedente);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Diario));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Diario, 1m, 100));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Diario, 1m, excedente: 1m));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Diario, -1m));
    }

    [TestMethod]
    public void ControladoExigeFranquiaEExcedenteESemPrecoKm()
    {
        var plano = Novo(ModalidadePlano.Controlado, franquia: 100, excedente: 1.05m);
        Assert.AreEqual(100, plano.FranquiaDiariaKm);
        Assert.AreEqual(1.05m, plano.PrecoKmExcedente);
        Assert.IsNull(plano.PrecoKm);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Controlado, excedente: 1m));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Controlado, franquia: -1, excedente: 1m));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Controlado, franquia: 100));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Controlado, franquia: 100, excedente: -1m));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Controlado, 1m, 100, 1m));
    }

    [TestMethod]
    public void LivreAceitaSomenteDiaria()
    {
        var plano = Novo(ModalidadePlano.Livre);
        Assert.IsNull(plano.PrecoKm);
        Assert.IsNull(plano.FranquiaDiariaKm);
        Assert.IsNull(plano.PrecoKmExcedente);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Livre, 0m));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Livre, franquia: 0));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo(ModalidadePlano.Livre, excedente: 0m));
    }

    [TestMethod]
    public void EdicaoInvalidaNaoAlteraPrecoNemInstanteDaVersao()
    {
        var plano = Novo(ModalidadePlano.Diario, 1m);
        var capturado = plano.PrecoDiario;
        plano.Alterar(150m, 2m, null, null, Agora.AddMinutes(1));

        Assert.AreEqual(100m, capturado);
        Assert.AreEqual(150m, plano.PrecoDiario);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => plano.Alterar(200m, -1m, null, null, Agora.AddMinutes(2)));
        Assert.AreEqual(150m, plano.PrecoDiario);
        Assert.AreEqual(2m, plano.PrecoKm);
        Assert.AreEqual(Agora.AddMinutes(1), plano.AlteradoEmUtc);
        plano.Excluir(Agora.AddMinutes(2));
        Assert.ThrowsExactly<InvalidOperationException>(() => plano.Alterar(300m, 1m, null, null, Agora.AddMinutes(3)));
    }

    [TestMethod]
    public void PlanoRejeitaGrupoAusenteModalidadeDesconhecidaEPrecoInvalido()
    {
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            Plano.Novo(Guid.NewGuid(), Guid.Empty, ModalidadePlano.Livre, 0m, null, null, null, Agora));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => Novo((ModalidadePlano)99));
        foreach (var preco in new[] { -1m, 0.0000001m, 10000000000000m })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
                Plano.Novo(Guid.NewGuid(), Guid.NewGuid(), ModalidadePlano.Livre, preco, null, null, null, Agora));
        }
    }

    private static Plano Novo(ModalidadePlano modalidade, decimal? precoKm = null, int? franquia = null, decimal? excedente = null) =>
        Plano.Novo(Guid.NewGuid(), Guid.NewGuid(), modalidade, 100m, precoKm, franquia, excedente, Agora);
}
