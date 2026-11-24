using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.Locacoes;

[TestClass]
public sealed class EdicaoTests
{
    [TestMethod]
    public async Task RevisaDentroDaUnidadeESaidaOriginalNaoMuda()
    {
        var consultas = new EstimativasTests.Consultas();
        var unidade = new ContratacaoTests.Unidade();
        var aluguel = Aluguel.Criar(consultas.Dados.Cliente.EmpresaId, consultas.Dados, new EstimativasTests.Relogio().Agora);
        typeof(EntidadeCadastro).GetProperty(nameof(EntidadeCadastro.RowVersion))!.SetValue(aluguel, ContratacaoTests.Repositorio.Versao.ToArray());
        var repositorio = new ContratacaoTests.Repositorio(unidade) { Aluguel = aluguel };
        var auditoria = new ContratacaoTests.Auditoria(unidade) { AcaoEsperada = AcaoCadastro.Alteracao };
        var resultado = await new CasosEdicaoAluguel(consultas, repositorio, unidade, auditoria,
            new EstimativasTests.Relogio { Agora = aluguel.SaidaUtc.AddHours(1) })
            .AlterarAsync(aluguel.Id, consultas.Comando, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None);
        Assert.AreEqual(2, resultado.Revisao.Numero);
        Assert.AreEqual(aluguel.SaidaUtc, resultado.Revisao.SaidaUtc);
        Assert.AreEqual(1, auditoria.Chamadas);
        Assert.AreEqual(1, unidade.Chamadas);
    }

    [TestMethod]
    public async Task EstadoNaoEditavelOuVersaoAusenteNaoCriaRevisaoNemAudita()
    {
        var consultas = new EstimativasTests.Consultas();
        var unidade = new ContratacaoTests.Unidade();
        var aluguel = Aluguel.Criar(consultas.Dados.Cliente.EmpresaId, consultas.Dados, new EstimativasTests.Relogio().Agora);
        typeof(EntidadeCadastro).GetProperty(nameof(EntidadeCadastro.RowVersion))!.SetValue(aluguel, ContratacaoTests.Repositorio.Versao.ToArray());
        var repositorio = new ContratacaoTests.Repositorio(unidade) { Aluguel = aluguel };
        var auditoria = new ContratacaoTests.Auditoria(unidade) { AcaoEsperada = AcaoCadastro.Alteracao };
        var casos = new CasosEdicaoAluguel(consultas, repositorio, unidade, auditoria, new EstimativasTests.Relogio());
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.AlterarAsync(aluguel.Id, consultas.Comando, null, CancellationToken.None));
        // Estado inválido exercita a guarda sem implementar conclusão/reabertura da F4.
        typeof(Aluguel).GetProperty(nameof(Aluguel.Estado))!.SetValue(aluguel, (EstadoAluguel)99);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.AlterarAsync(aluguel.Id, consultas.Comando, VersaoCadastro.Etag(aluguel.RowVersion), CancellationToken.None));
        Assert.HasCount(1, aluguel.Revisoes);
        Assert.AreEqual(0, auditoria.Chamadas);
        Assert.AreEqual(0, consultas.Chamadas);
    }
}
