using System.Data.Common;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static Locadora.IntegrationTests.Modulos.Locacoes.ConclusaoDevolucaoSqlTests;
using static Locadora.IntegrationTests.Modulos.Locacoes.PersistenciaDevolucaoSqlTests;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class IdempotenciaDevolucaoSqlTests
{
    [TestMethod]
    public async Task CommitIncertoNaoRepeteTransacaoEReplayRecuperaMesmoFinalEVersao()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var (aluguel, comando, versao) = await PrepararAsync(cenario);
        var falha = new FalhaAposCommit();
        var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>().UseSqlServer(AmbienteIntegracao.Conexao)
            .AddInterceptors(new GuardaEscritaEmpresa(cenario.Contexto), falha).Options;
        await using var banco = new LocadoraDbContext(opcoes, cenario.Contexto);
        Assert.IsFalse(banco.Database.CreateExecutionStrategy().RetriesOnFailure);
        var chave = Guid.NewGuid();
        await Assert.ThrowsExactlyAsync<TimeoutException>(() => Casos(banco, cenario.Contexto)
            .ConcluirAsync(aluguel.Id, comando, versao, chave, CancellationToken.None));
        Assert.AreEqual(1, falha.Commits);
        var original = await cenario.Banco.ComandosIdempotentes.AsNoTracking().SingleAsync(c => c.Operacao == "devolver-aluguel");
        var replay = await Casos(cenario.Banco, cenario.Contexto).ConcluirAsync(aluguel.Id, comando, versao, chave, CancellationToken.None);
        Assert.AreEqual(original.DemonstrativoFinalId, replay.Demonstrativo.Id);
        CollectionAssert.AreEqual(original.VersaoOriginal, replay.RowVersion);
        Assert.AreEqual(334m, replay.Demonstrativo.TotalFinal);
        Assert.AreEqual(1, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Devolucao));
    }

    [TestMethod]
    public async Task ReplayAposOcultacaoReajusteENovaOcupacaoNaoAlteraDisponibilidadeOuHistorico()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var (aluguel, comando, versao) = await PrepararAsync(cenario);
        var chave = Guid.NewGuid();
        var casos = Casos(cenario.Banco, cenario.Contexto);
        var resultado = await casos.ConcluirAsync(aluguel.Id, comando, versao, chave, CancellationToken.None);
        await Unidade(cenario).ExecutarAsync(async token =>
        {
            var atual = await ContratosComFinal(cenario.Banco).SingleAsync(token);
            Assert.IsTrue(atual.Ocultar(CenarioLocacoesSql.Agora.AddDays(2)));
            cenario.Banco.Add(Auditoria(cenario, aluguel.Id, AcaoCadastro.Ocultacao));
            var taxa = await cenario.Banco.TaxasServicos.SingleAsync(t => t.Id == comando.Dados.TaxasAdicionaisIds.Single(), token);
            taxa.Alterar("Nome/preço posterior", 999m, Domain.Modulos.FrotaPrecos.ModalidadeTaxa.Diaria, CenarioLocacoesSql.Agora.AddDays(2));
            return 0;
        }, CancellationToken.None);
        var nova = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto, new Relogio())
            .CriarAsync(EstimativasSelecoesTests.Comando(cenario) with
            { RetornoPrevisto = CenarioLocacoesSql.Agora.AddDays(4) }, Guid.NewGuid(), CancellationToken.None);
        var canonico = comando with
        {
            Dados = comando.Dados with { RetornoReal = comando.Dados.RetornoReal.ToOffset(TimeSpan.FromHours(-3)),
                LitrosRestantes = 40.000m, TaxasAdicionaisIds = comando.Dados.TaxasAdicionaisIds.Reverse().ToArray() },
            VersoesAceitas = comando.VersoesAceitas with { TaxasNovas = comando.VersoesAceitas.TaxasNovas.Reverse().ToArray() },
        };
        var replay = await casos.ConcluirAsync(aluguel.Id, canonico, versao, chave, CancellationToken.None);
        Assert.AreEqual(resultado.Demonstrativo.Id, replay.Demonstrativo.Id);
        Assert.AreEqual(resultado.Demonstrativo.TotalFinal, replay.Demonstrativo.TotalFinal);
        CollectionAssert.AreEqual(resultado.RowVersion, replay.RowVersion);
        Assert.IsNull(await new RepositorioAlugueis(cenario.Banco).ConsultarAsync(aluguel.Id, CancellationToken.None));
        Assert.AreEqual(EstadoAluguel.EmAberto, (await cenario.Banco.Alugueis.AsNoTracking().SingleAsync(a => a.Id == nova.AluguelId)).Estado);
        Assert.IsTrue(await new ConsultasContratacao(cenario.Banco).VeiculoOcupadoAsync(aluguel.VeiculoId, null, CancellationToken.None));
        Assert.AreEqual(1, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Devolucao));
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Ocultacao));
    }

    [TestMethod]
    public async Task ChaveReutilizadaComOutroCorpoVersaoOuRecursoConflitaENovaIntencaoNaoConcluiDeNovo()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var (aluguel, comando, versao) = await PrepararAsync(cenario);
        var chave = Guid.NewGuid();
        var casos = Casos(cenario.Banco, cenario.Contexto);
        var resultado = await casos.ConcluirAsync(aluguel.Id, comando, versao, chave, CancellationToken.None);
        foreach (var alterado in new[]
        {
            comando with { Dados = comando.Dados with { HodometroFinal = comando.Dados.HodometroFinal + 1 } },
            comando with { Dados = comando.Dados with { LitrosRestantes = 39m } },
            comando with { VersoesAceitas = comando.VersoesAceitas with { ResumoCalculo = new string('0', 64) } },
        })
        {
            var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
                casos.ConcluirAsync(aluguel.Id, alterado, versao, chave, CancellationToken.None));
            Assert.AreEqual("chave_idempotente_reutilizada", erro.Codigo);
        }
        var recurso = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.ConcluirAsync(Guid.NewGuid(), comando, versao, chave, CancellationToken.None));
        Assert.AreEqual("chave_idempotente_reutilizada", recurso.Codigo);
        var outraVersao = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.ConcluirAsync(aluguel.Id, comando, VersaoCadastro.Etag(resultado.RowVersion), chave, CancellationToken.None));
        Assert.AreEqual("chave_idempotente_reutilizada", outraVersao.Codigo);
        var nova = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.ConcluirAsync(aluguel.Id, comando, VersaoCadastro.Etag(resultado.RowVersion), Guid.NewGuid(), CancellationToken.None));
        Assert.AreEqual("aluguel_nao_devolvivel", nova.Codigo);
        Assert.AreEqual(1, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task MesmaChaveConcorrenteRetornaUmUnicoResultadoENaoCompartilhaEmpresaOuAtor()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var (aluguel, comando, versao) = await PrepararAsync(cenario);
        var chave = Guid.NewGuid();
        await using var banco1 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        await using var banco2 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ResultadoDevolucao> Devolver(LocadoraDbContext banco)
        {
            await inicio.Task;
            return await Casos(banco, cenario.Contexto).ConcluirAsync(aluguel.Id, comando, versao, chave, CancellationToken.None);
        }
        var um = Devolver(banco1);
        var dois = Devolver(banco2);
        inicio.SetResult();
        var resultados = await Task.WhenAll(um, dois);
        Assert.AreEqual(resultados[0].Demonstrativo.Id, resultados[1].Demonstrativo.Id);
        CollectionAssert.AreEqual(resultados[0].RowVersion, resultados[1].RowVersion);
        await using var b = await CenarioLocacoesSql.CriarAsync();
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            Casos(b.Banco, b.Contexto).ConcluirAsync(aluguel.Id, comando, versao, chave, CancellationToken.None));
        var outroAtor = new ContextoEmpresa
        {
            EmpresaId = cenario.Contexto.EmpresaId, ContaId = Guid.NewGuid(), CorrelacaoId = Guid.NewGuid(),
        };
        await using var bancoAtor = AmbienteIntegracao.CriarContextoComGuarda(outroAtor);
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            Casos(bancoAtor, outroAtor).ConcluirAsync(aluguel.Id, comando, versao, chave, CancellationToken.None));
        Assert.AreEqual(1, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync());
    }

    private sealed class FalhaAposCommit : DbTransactionInterceptor
    {
        public int Commits { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Commits++;
            throw new TimeoutException("Resposta sintética perdida após commit SQL.");
        }
    }
}
