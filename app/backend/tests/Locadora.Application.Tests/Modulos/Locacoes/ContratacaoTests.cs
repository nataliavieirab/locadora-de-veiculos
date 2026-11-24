using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.Locacoes;

[TestClass]
public sealed class ContratacaoTests
{
    [TestMethod]
    public async Task CriaAuditaERegistraResultadoDentroDaUnidade()
    {
        var consultas = new EstimativasTests.Consultas();
        var unidade = new Unidade();
        var repositorio = new Repositorio(unidade);
        var auditoria = new Auditoria(unidade);
        var contexto = new Contexto(consultas.Dados.Cliente.EmpresaId, Guid.NewGuid());
        var chave = Guid.NewGuid();
        var resultado = await new CasosContratacao(consultas, repositorio, unidade, auditoria, contexto,
            new EstimativasTests.Relogio()).CriarAsync(consultas.Comando, chave, CancellationToken.None);
        Assert.AreEqual(1, unidade.Chamadas);
        Assert.AreEqual(1, auditoria.Chamadas);
        Assert.AreEqual(resultado.AluguelId, repositorio.Aluguel!.Id);
        Assert.AreEqual(200m, resultado.Revisao.Estimativa.TotalConhecido);
        Assert.AreEqual(chave, repositorio.Comando!.Chave);
        Assert.AreEqual(contexto.ContaId, repositorio.Comando.ContaId);
        CollectionAssert.AreEqual(Repositorio.Versao, resultado.RowVersion);
        Assert.IsFalse(unidade.EmTransacao);
    }

    [TestMethod]
    public async Task EntradaInvalidaNaoAbreUnidadeEOcupadoNaoEscreve()
    {
        var consultas = new EstimativasTests.Consultas { Ocupado = true };
        var unidade = new Unidade();
        var repositorio = new Repositorio(unidade);
        var auditoria = new Auditoria(unidade);
        var casos = new CasosContratacao(consultas, repositorio, unidade, auditoria,
            new Contexto(consultas.Dados.Cliente.EmpresaId, Guid.NewGuid()), new EstimativasTests.Relogio());
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            casos.CriarAsync(consultas.Comando, Guid.Empty, CancellationToken.None));
        Assert.AreEqual(0, unidade.Chamadas);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.CriarAsync(consultas.Comando, Guid.NewGuid(), CancellationToken.None));
        Assert.IsNull(repositorio.Aluguel);
        Assert.IsNull(repositorio.Comando);
        Assert.AreEqual(0, auditoria.Chamadas);
    }

    private sealed record Contexto(Guid EmpresaId, Guid ContaId) : IContextoLocacao;

    internal sealed class Unidade : IUnidadeTrabalhoEmpresa
    {
        public bool EmTransacao { get; private set; }
        public int Chamadas { get; private set; }
        public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento)
        {
            Chamadas++;
            EmTransacao = true;
            try { return await comando(cancelamento); }
            finally { EmTransacao = false; }
        }
    }

    internal sealed class Repositorio(Unidade unidade) : IRepositorioAlugueis
    {
        public static readonly byte[] Versao = [0, 0, 0, 0, 0, 0, 0, 1];
        public Aluguel? Aluguel { get; set; }
        public ComandoIdempotente? Comando { get; private set; }
        public Task<Aluguel?> ConsultarAsync(Guid id, CancellationToken cancelamento) => Task.FromResult(Aluguel);
        public Task<ComandoIdempotente?> ComandoAsync(Guid contaId, Guid chave, CancellationToken cancelamento)
        {
            Assert.IsTrue(unidade.EmTransacao);
            return Task.FromResult(Comando?.ContaId == contaId && Comando.Chave == chave ? Comando : null);
        }
        public Task<RevisaoContrato> RevisaoAsync(Guid id, CancellationToken cancelamento) => Task.FromResult(Aluguel!.Revisoes.Single(r => r.Id == id));
        public void Adicionar(Aluguel aluguel) { Assert.IsTrue(unidade.EmTransacao); Aluguel = aluguel; }
        public void Adicionar(ComandoIdempotente comando) { Assert.IsTrue(unidade.EmTransacao); Comando = comando; }
        public Task<byte[]> SalvarAsync(Aluguel aluguel, CancellationToken cancelamento)
        {
            Assert.IsTrue(unidade.EmTransacao);
            return Task.FromResult(Versao.ToArray());
        }
    }

    internal sealed class Auditoria(Unidade unidade) : IAuditoriaCadastro
    {
        public int Chamadas { get; private set; }
        public AcaoCadastro AcaoEsperada { get; set; } = AcaoCadastro.Criacao;
        public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid entidadeId, params string[] campos)
        {
            Assert.IsTrue(unidade.EmTransacao);
            Assert.AreEqual(AcaoEsperada, acao);
            Assert.AreEqual(TipoCadastro.Aluguel, entidade);
            Chamadas++;
        }
    }
}
