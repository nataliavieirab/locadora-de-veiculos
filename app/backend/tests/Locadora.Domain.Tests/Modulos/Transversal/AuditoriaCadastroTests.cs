using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.Transversal;

[TestClass]
public sealed class AuditoriaCadastroTests
{
    [TestMethod]
    public void EventoContemSomenteIdentificadoresENomesDosCampos()
    {
        var empresa = Guid.NewGuid();
        var conta = Guid.NewGuid();
        var entidade = Guid.NewGuid();
        var correlacao = Guid.NewGuid();
        var agora = new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.FromHours(-3));
        var auditoria = AuditoriaCadastro.Registrar(empresa, conta, agora,
            AcaoCadastro.Alteracao, TipoCadastro.Cliente, entidade, correlacao, ["nome", "cpf", "nome"]);

        Assert.AreEqual(empresa, auditoria.EmpresaId);
        Assert.AreEqual(conta, auditoria.ContaId);
        Assert.AreEqual(entidade, auditoria.EntidadeId);
        Assert.AreEqual(correlacao, auditoria.CorrelacaoId);
        Assert.AreEqual("cpf,nome", auditoria.CamposAlterados);
        Assert.AreEqual(TimeSpan.Zero, auditoria.MomentoUtc.Offset);
        Assert.IsFalse(typeof(AuditoriaCadastro).GetProperties().Any(item => item.Name.Contains("Valor", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void EventoRejeitaValoresOuCamposNaoPermitidosSemRefletiLosNaMensagem()
    {
        foreach (var campos in new[] { Array.Empty<string>(), ["12300000032"], ["nome=Pessoa sintética"], ["password"], ["accessToken"] })
        {
            var erro = Assert.ThrowsExactly<ArgumentException>(() =>
                AuditoriaCadastro.Registrar(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
                    AcaoCadastro.Criacao, TipoCadastro.Cliente, Guid.NewGuid(), Guid.NewGuid(), campos));
            Assert.AreEqual("A auditoria aceita somente nomes de campos cadastrais.", erro.Message);
        }
    }

    [TestMethod]
    public void EventoRejeitaIdentificadoresAusentes()
    {
        for (var indice = 0; indice < 4; indice++)
        {
            var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
            ids[indice] = Guid.Empty;
            Assert.ThrowsExactly<ArgumentException>(() =>
                AuditoriaCadastro.Registrar(ids[0], ids[1], DateTimeOffset.UtcNow, AcaoCadastro.Criacao,
                    TipoCadastro.Grupo, ids[2], ids[3], ["nome"]));
        }
    }
}
