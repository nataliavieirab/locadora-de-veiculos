using System.Data.Common;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class IdempotenciaLocacoesTests
{
    [TestMethod]
    public async Task ReplayCanonicoMantemRevisaoOriginalAposEdicaoEReajuste()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var outraTaxa = TaxaServico.Nova(cenario.Contexto.EmpresaId!.Value, "Outra taxa", 10, ModalidadeTaxa.Diaria, CenarioLocacoesSql.Agora);
        cenario.Banco.Add(outraTaxa);
        await cenario.Banco.SaveChangesAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario) with { TaxasServicoIds = [cenario.Dados.Taxas.Single().Id, outraTaxa.Id] };
        var chave = Guid.NewGuid();
        var casos = ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto);
        var primeira = await casos.CriarAsync(comando, chave, CancellationToken.None);
        var reordenado = comando with
        {
            TaxasServicoIds = comando.TaxasServicoIds.Reverse().ToArray(),
            Seguros = comando.Seguros.Reverse().ToArray(),
            RetornoPrevisto = comando.RetornoPrevisto.ToOffset(TimeSpan.FromHours(-3)),
        };
        var replay = await casos.CriarAsync(reordenado, chave, CancellationToken.None);
        Assert.AreEqual(primeira.Revisao.Id, replay.Revisao.Id);
        CollectionAssert.AreEqual(primeira.RowVersion, replay.RowVersion);
        var unidade = new UnidadeTrabalhoEmpresa(cenario.Banco, cenario.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
        await unidade.ExecutarAsync(async token =>
        {
            var aluguel = await new RepositorioAlugueis(cenario.Banco).ConsultarAsync(primeira.AluguelId, token);
            var dados = await new ConsultasContratacao(cenario.Banco).CarregarAsync(comando, token);
            aluguel!.Editar(dados with { RetornoPrevisto = dados.RetornoPrevisto.AddDays(1) }, CenarioLocacoesSql.Agora.AddMinutes(1));
            cenario.Banco.Add(AuditoriaCadastro.Registrar(aluguel.EmpresaId, cenario.Contexto.ContaId!.Value,
                CenarioLocacoesSql.Agora.AddMinutes(1), AcaoCadastro.Alteracao, TipoCadastro.Aluguel, aluguel.Id, Guid.NewGuid(), ["revisao"]));
            return true;
        }, CancellationToken.None);
        var plano = await cenario.Banco.Planos.SingleAsync();
        plano.Alterar(900, null, 100, 2, CenarioLocacoesSql.Agora.AddMinutes(2));
        await cenario.Banco.SaveChangesAsync();
        var repetida = await casos.CriarAsync(comando, chave, CancellationToken.None);
        Assert.AreEqual(1, repetida.Revisao.Numero);
        Assert.AreEqual(primeira.Revisao.Id, repetida.Revisao.Id);
        Assert.AreEqual(primeira.Revisao.Estimativa.TotalConhecido, repetida.Revisao.Estimativa.TotalConhecido);
        CollectionAssert.AreEqual(primeira.RowVersion, repetida.RowVersion);
        Assert.AreEqual(2, (await cenario.Banco.Alugueis.SingleAsync()).NumeroRevisaoAtual);
        Assert.AreEqual(2, await cenario.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.ComandosIdempotentes.CountAsync());
    }

    [TestMethod]
    public async Task ChaveOutroConteudoConflitaEChaveDistintaNaoReplayaVeiculoOcupado()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var casos = ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto);
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var chave = Guid.NewGuid();
        await casos.CriarAsync(comando, chave, CancellationToken.None);
        var diferente = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.CriarAsync(comando with { HodometroInicial = 99 }, chave, CancellationToken.None));
        Assert.AreEqual("chave_idempotente_reutilizada", diferente.Codigo);
        var outraChave = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.CriarAsync(comando, Guid.NewGuid(), CancellationToken.None));
        Assert.AreEqual("veiculo_indisponivel", outraChave.Codigo);
        var repositorio = new RepositorioAlugueis(cenario.Banco);
        Assert.IsNull(await repositorio.ComandoAsync(Guid.NewGuid(), chave, CancellationToken.None));
        Assert.AreEqual(1, await cenario.Banco.Alugueis.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.ComandosIdempotentes.CountAsync());
    }

    [TestMethod]
    public async Task MesmaChaveEmEmpresasEAtoresDistintosNaoCompartilhaResultado()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var chave = Guid.NewGuid();
        var resultadoA = await ContratacaoSqlTests.Casos(a.Banco, a.Contexto)
            .CriarAsync(EstimativasSelecoesTests.Comando(a), chave, CancellationToken.None);
        var resultadoB = await ContratacaoSqlTests.Casos(b.Banco, b.Contexto)
            .CriarAsync(EstimativasSelecoesTests.Comando(b), chave, CancellationToken.None);
        Assert.AreNotEqual(resultadoA.AluguelId, resultadoB.AluguelId);
        Assert.AreNotEqual(a.Contexto.ContaId, b.Contexto.ContaId);
        Assert.AreEqual(a.Contexto.EmpresaId, (await a.Banco.ComandosIdempotentes.SingleAsync()).EmpresaId);
        Assert.AreEqual(b.Contexto.EmpresaId, (await b.Banco.ComandosIdempotentes.SingleAsync()).EmpresaId);
    }

    [TestMethod]
    public async Task DuasCriacoesMesmaChaveEmConexoesIndependentesRetornamMesmoResultado()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        await using var banco1 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        await using var banco2 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var chave = Guid.NewGuid();
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ResultadoContratacao> Criar(LocadoraDbContext banco)
        {
            await inicio.Task;
            return await ContratacaoSqlTests.Casos(banco, cenario.Contexto).CriarAsync(comando, chave, CancellationToken.None);
        }
        var primeira = Criar(banco1);
        var segunda = Criar(banco2);
        inicio.SetResult();
        var resultados = await Task.WhenAll(primeira, segunda);
        Assert.AreEqual(resultados[0].AluguelId, resultados[1].AluguelId);
        Assert.AreEqual(resultados[0].Revisao.Id, resultados[1].Revisao.Id);
        CollectionAssert.AreEqual(resultados[0].RowVersion, resultados[1].RowVersion);
        Assert.AreEqual(1, await cenario.Banco.Alugueis.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.ComandosIdempotentes.CountAsync());
    }

    [TestMethod]
    public async Task RespostaPerdidaDepoisDoCommitNaoRetryaEReplayRecuperaSucesso()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var gancho = new FalhaAposCommit();
        var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>().UseSqlServer(AmbienteIntegracao.Conexao)
            .AddInterceptors(new GuardaEscritaEmpresa(cenario.Contexto), gancho).Options;
        await using var banco = new LocadoraDbContext(opcoes, cenario.Contexto);
        Assert.IsFalse(banco.Database.CreateExecutionStrategy().RetriesOnFailure);
        var chave = Guid.NewGuid();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            ContratacaoSqlTests.Casos(banco, cenario.Contexto).CriarAsync(comando, chave, CancellationToken.None));
        Assert.AreEqual(1, gancho.Commits);
        var replay = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto)
            .CriarAsync(comando, chave, CancellationToken.None);
        Assert.AreEqual(1, replay.Revisao.Numero);
        Assert.AreEqual(1, await cenario.Banco.Alugueis.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync());
        var resultado = await cenario.Banco.ComandosIdempotentes.SingleAsync();
        Assert.AreEqual(replay.Revisao.Id, resultado.RevisaoId);
        var versao = resultado.VersaoOriginal;
        versao[0] ^= 1;
        CollectionAssert.AreEqual(replay.RowVersion, resultado.VersaoOriginal);
    }

    private sealed class FalhaAposCommit : DbTransactionInterceptor
    {
        public int Commits { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Commits++;
            throw new TimeoutException("Resposta sintética perdida após o commit SQL.");
        }
    }
}
