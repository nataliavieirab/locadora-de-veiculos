using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.Transversal;

[TestClass]
public sealed class CadastroTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(-3));

    [TestMethod]
    public void NovoCadastroTemIdentidadeEmpresaEInstantesUtc()
    {
        var empresaId = Guid.NewGuid();
        var cadastro = new CadastroTeste(empresaId, Agora);

        Assert.AreNotEqual(Guid.Empty, cadastro.Id);
        Assert.AreEqual(empresaId, ((IEntidadeEmpresa)cadastro).EmpresaId);
        Assert.AreEqual(Agora.ToUniversalTime(), cadastro.CriadoEmUtc);
        Assert.AreEqual(TimeSpan.Zero, cadastro.CriadoEmUtc.Offset);
        Assert.AreEqual(cadastro.CriadoEmUtc, cadastro.AlteradoEmUtc);
        Assert.IsNull(cadastro.ExcluidoEmUtc);
        Assert.IsEmpty(cadastro.RowVersion);
    }

    [TestMethod]
    public void CadastroSemEmpresaNaoPodeSerCriado()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CadastroTeste(Guid.Empty, Agora));
    }

    [TestMethod]
    public void AlteracaoMantemIdentidadeEmpresaECriacao()
    {
        var empresaId = Guid.NewGuid();
        var cadastro = new CadastroTeste(empresaId, Agora);
        var id = cadastro.Id;

        cadastro.Alterar(Agora.AddMinutes(1));

        Assert.AreEqual(id, cadastro.Id);
        Assert.AreEqual(empresaId, cadastro.EmpresaId);
        Assert.AreEqual(Agora.ToUniversalTime(), cadastro.CriadoEmUtc);
        Assert.AreEqual(Agora.AddMinutes(1).ToUniversalTime(), cadastro.AlteradoEmUtc);
        Assert.AreEqual(TimeSpan.Zero, cadastro.AlteradoEmUtc.Offset);
        Assert.IsEmpty(cadastro.RowVersion);
    }

    [TestMethod]
    public void ExclusaoPreservaIdentidadeEMarcaInstanteSemRestauracao()
    {
        var cadastro = new CadastroTeste(Guid.NewGuid(), Agora);
        var id = cadastro.Id;
        var exclusao = Agora.AddMinutes(2);

        cadastro.Excluir(exclusao);

        Assert.AreEqual(id, cadastro.Id);
        Assert.AreEqual(exclusao.ToUniversalTime(), cadastro.ExcluidoEmUtc);
        Assert.AreEqual(cadastro.ExcluidoEmUtc, cadastro.AlteradoEmUtc);
        Assert.ThrowsExactly<InvalidOperationException>(() => cadastro.Alterar(exclusao.AddMinutes(1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => cadastro.Excluir(exclusao.AddMinutes(1)));
        Assert.AreEqual(exclusao.ToUniversalTime(), cadastro.ExcluidoEmUtc);
    }

    [TestMethod]
    public void AlteracaoOuExclusaoAnteriorNaoMudaOsInstantes()
    {
        var cadastro = new CadastroTeste(Guid.NewGuid(), Agora);
        cadastro.Alterar(Agora.AddMinutes(2));

        Assert.ThrowsExactly<ArgumentException>(() => cadastro.Alterar(Agora.AddMinutes(1)));
        Assert.ThrowsExactly<ArgumentException>(() => cadastro.Excluir(Agora.AddMinutes(1)));

        Assert.AreEqual(Agora.AddMinutes(2).ToUniversalTime(), cadastro.AlteradoEmUtc);
        Assert.IsNull(cadastro.ExcluidoEmUtc);
    }

    [TestMethod]
    public void OutroCadastroDaMesmaEmpresaRecebeOutraIdentidade()
    {
        var empresaId = Guid.NewGuid();
        var anterior = new CadastroTeste(empresaId, Agora);
        anterior.Excluir(Agora);
        var novo = new CadastroTeste(empresaId, Agora);

        Assert.AreNotEqual(anterior.Id, novo.Id);
        Assert.IsNull(novo.ExcluidoEmUtc);
    }

    [TestMethod]
    public void MutacaoNoMesmoInstanteEPermitidaSemFabricarRowversion()
    {
        var cadastro = new CadastroTeste(Guid.NewGuid(), Agora);

        cadastro.Alterar(Agora);
        cadastro.Excluir(Agora);

        Assert.AreEqual(Agora.ToUniversalTime(), cadastro.ExcluidoEmUtc);
        Assert.IsEmpty(cadastro.RowVersion);
    }

    private sealed class CadastroTeste(Guid empresaId, DateTimeOffset agora) : CadastroExcluivel(empresaId, agora)
    {
        public void Alterar(DateTimeOffset agora)
        {
            ExigirAtivo();
            RegistrarAlteracao(agora);
        }
    }
}
