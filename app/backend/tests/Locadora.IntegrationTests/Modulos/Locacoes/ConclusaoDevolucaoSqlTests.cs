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
using Microsoft.Extensions.Logging.Abstractions;
using static Locadora.IntegrationTests.Modulos.Locacoes.PersistenciaDevolucaoSqlTests;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class ConclusaoDevolucaoSqlTests
{
    [TestMethod]
    public async Task ConcluiComResultadoAuditoriaEFinalNoMesmoCommitELiberaSomenteSeuAberto()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var (aluguel, comando, versao) = await PrepararAsync(cenario);
        var resultado = await Casos(cenario.Banco, cenario.Contexto).ConcluirAsync(
            aluguel.Id, comando, versao, Guid.NewGuid(), CancellationToken.None);
        Assert.AreEqual(334m, resultado.Demonstrativo.TotalFinal);
        Assert.AreEqual(aluguel.RevisaoAtual.Id, resultado.Demonstrativo.RevisaoId);
        cenario.Banco.ChangeTracker.Clear();
        var lido = await ContratosComFinal(cenario.Banco).SingleAsync();
        Assert.AreEqual(EstadoAluguel.Concluido, lido.Estado);
        CollectionAssert.AreEqual(resultado.RowVersion, lido.RowVersion);
        Assert.AreEqual(1, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.ItensDevolucao.CountAsync());
        Assert.AreEqual(7, await cenario.Banco.ComponentesFinal.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Devolucao));
        Assert.AreEqual(2, await cenario.Banco.ComandosIdempotentes.CountAsync());
        Assert.IsFalse(await new ConsultasContratacao(cenario.Banco).VeiculoOcupadoAsync(aluguel.VeiculoId, null, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("DemonstrativosFinais")]
    [DataRow("ItensDevolucao")]
    [DataRow("ComponentesFinal")]
    [DataRow("Alugueis")]
    [DataRow("AuditoriasCadastro")]
    [DataRow("ComandosIdempotentes")]
    public async Task FalhaAntesDeLoteDeCadaEtapaMantemAbertoSemFinalAuditoriaOuResultado(string tabela)
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var (aluguel, comando, versao) = await PrepararAsync(cenario);
        var falha = new FalhaAntesLote(tabela);
        var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>().UseSqlServer(AmbienteIntegracao.Conexao)
            .AddInterceptors(new GuardaEscritaEmpresa(cenario.Contexto), falha).Options;
        await using var banco = new LocadoraDbContext(opcoes, cenario.Contexto);
        Exception? capturada = null;
        try
        {
            await Casos(banco, cenario.Contexto).ConcluirAsync(aluguel.Id, comando, versao, Guid.NewGuid(), CancellationToken.None);
        }
        catch (Exception erro) { capturada = erro; }
        Assert.IsNotNull(capturada);
        Assert.IsTrue(capturada is FalhaSinteticaException || capturada.InnerException is FalhaSinteticaException);
        Assert.AreEqual(1, falha.Disparos);
        await ExigirRollbackAsync(cenario, aluguel.Id);
    }

    [TestMethod]
    public async Task FalhaDepoisDeCadaSaveComSqlExecutadoReverteInclusiveOResultado()
    {
        foreach (var falharNoSave in new[] { 1, 2 })
        {
            await using var cenario = await CenarioLocacoesSql.CriarAsync();
            var (aluguel, comando, versao) = await PrepararAsync(cenario);
            var falha = new FalhaDepoisSave(falharNoSave);
            var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>().UseSqlServer(AmbienteIntegracao.Conexao)
                .AddInterceptors(new GuardaEscritaEmpresa(cenario.Contexto), falha).Options;
            await using var banco = new LocadoraDbContext(opcoes, cenario.Contexto);
            await Assert.ThrowsExactlyAsync<FalhaSinteticaException>(() => Casos(banco, cenario.Contexto)
                .ConcluirAsync(aluguel.Id, comando, versao, Guid.NewGuid(), CancellationToken.None));
            Assert.AreEqual(falharNoSave, falha.Chamadas);
            await ExigirRollbackAsync(cenario, aluguel.Id);
        }
    }

    [TestMethod]
    public async Task DuasDevolucoesComChavesDistintasEmConexoesIndependentesProduzemUmFinal()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var (aluguel, comando, versao) = await PrepararAsync(cenario);
        await using var banco1 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        await using var banco2 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        Assert.AreNotSame(banco1.Database.GetDbConnection(), banco2.Database.GetDbConnection());
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<object> Tentar(LocadoraDbContext banco)
        {
            await inicio.Task;
            try { return await Casos(banco, cenario.Contexto).ConcluirAsync(aluguel.Id, comando, versao, Guid.NewGuid(), CancellationToken.None); }
            catch (PrecondicaoCadastroException erro) { Assert.IsFalse(erro.Ausente); return erro; }
        }
        var um = Tentar(banco1);
        var dois = Tentar(banco2);
        inicio.SetResult();
        var resultados = await Task.WhenAll(um, dois);
        Assert.AreEqual(1, resultados.Count(r => r is ResultadoDevolucao));
        Assert.AreEqual(1, resultados.Count(r => r is PrecondicaoCadastroException));
        Assert.AreEqual(1, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Devolucao));
    }

    [TestMethod]
    public async Task EdicaoContraConclusaoEmConexoesIndependentesNaoPerdeRevisaoOuFinal()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var (aluguel, comando, versao) = await PrepararAsync(cenario);
        await using var banco1 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        await using var banco2 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<object> Devolver()
        {
            await inicio.Task;
            try { return await Casos(banco1, cenario.Contexto).ConcluirAsync(aluguel.Id, comando, versao, Guid.NewGuid(), CancellationToken.None); }
            catch (PrecondicaoCadastroException erro) { return erro; }
        }
        async Task<object> Editar()
        {
            await inicio.Task;
            try { return await EdicaoLocacoesTests.Casos(banco2, cenario.Contexto).AlterarAsync(aluguel.Id,
                EstimativasSelecoesTests.Comando(cenario) with { Seguros = [] }, versao, CancellationToken.None); }
            catch (PrecondicaoCadastroException erro) { return erro; }
        }
        var um = Devolver();
        var dois = Editar();
        inicio.SetResult();
        var resultados = await Task.WhenAll(um, dois);
        Assert.AreEqual(1, resultados.Count(r => r is PrecondicaoCadastroException));
        cenario.Banco.ChangeTracker.Clear();
        var atual = await ContratosComFinal(cenario.Banco).SingleAsync();
        Assert.AreEqual(atual.Estado == EstadoAluguel.Concluido ? 1 : 0, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(atual.Estado == EstadoAluguel.Concluido ? 1 : 2, atual.NumeroRevisaoAtual);
    }

    [TestMethod]
    public async Task NovaContratacaoContraConclusaoNaoProduzDoisAbertosNemLiberaNovaOcupacao()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var (aluguel, comando, versao) = await PrepararAsync(cenario);
        await using var banco1 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        await using var banco2 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ResultadoDevolucao> Devolver()
        {
            await inicio.Task;
            return await Casos(banco1, cenario.Contexto).ConcluirAsync(aluguel.Id, comando, versao, Guid.NewGuid(), CancellationToken.None);
        }
        async Task<bool> Contratar()
        {
            await inicio.Task;
            try
            {
                await ContratacaoSqlTests.Casos(banco2, cenario.Contexto, new Relogio())
                    .CriarAsync(EstimativasSelecoesTests.Comando(cenario) with
                    { RetornoPrevisto = CenarioLocacoesSql.Agora.AddDays(4) }, Guid.NewGuid(), CancellationToken.None);
                return true;
            }
            catch (ConflitoCadastroException erro) { Assert.AreEqual("veiculo_indisponivel", erro.Codigo); return false; }
        }
        var devolver = Devolver();
        var contratar = Contratar();
        inicio.SetResult();
        await devolver;
        var contratado = await contratar;
        cenario.Banco.ChangeTracker.Clear();
        Assert.AreEqual(EstadoAluguel.Concluido, (await cenario.Banco.Alugueis.SingleAsync(a => a.Id == aluguel.Id)).Estado);
        Assert.AreEqual(contratado ? 1 : 0, await cenario.Banco.Alugueis.CountAsync(a => a.Estado == EstadoAluguel.EmAberto));
        Assert.AreEqual(1, await cenario.Banco.DemonstrativosFinais.CountAsync());
    }

    internal static CasosDevolucao Casos(LocadoraDbContext banco, ContextoEmpresa contexto, TimeProvider? tempo = null)
    {
        tempo ??= new Relogio();
        return new(new PreviaDevolucao(new ConsultasDevolucao(banco,
                new Infrastructure.Modulos.FrotaPrecos.RepositorioPrecosCombustiveis(banco), contexto), contexto),
            new RepositorioAlugueis(banco), new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
            new RegistroAuditoriaCadastro(banco, contexto, tempo), contexto, tempo);
    }

    internal static async Task<(Aluguel Aluguel, ComandoConclusaoDevolucao Comando, string Versao)> PrepararAsync(CenarioLocacoesSql cenario)
    {
        var aluguel = await CriarAbertoAsync(cenario);
        var dados = await PrepararDadosAsync(cenario);
        var versao = VersaoCadastro.Etag(aluguel.RowVersion);
        var comando = PreviaDevolucaoSqlTests.Comando(dados);
        var previa = await Casos(cenario.Banco, cenario.Contexto).PreviaAsync(aluguel.Id, comando, versao, CancellationToken.None);
        return (aluguel, new(comando, previa.VersoesAceitas), versao);
    }

    private static async Task ExigirRollbackAsync(CenarioLocacoesSql cenario, Guid id)
    {
        cenario.Banco.ChangeTracker.Clear();
        Assert.AreEqual(EstadoAluguel.EmAberto, (await cenario.Banco.Alugueis.SingleAsync(a => a.Id == id)).Estado);
        Assert.AreEqual(0, await cenario.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(0, await cenario.Banco.ItensDevolucao.CountAsync());
        Assert.AreEqual(0, await cenario.Banco.ComponentesFinal.CountAsync());
        Assert.AreEqual(0, await cenario.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.ComandosIdempotentes.CountAsync());
        Assert.IsTrue(await new ConsultasContratacao(cenario.Banco).VeiculoOcupadoAsync(cenario.Dados.Veiculo.Id, null, CancellationToken.None));
    }

    internal sealed class Relogio : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => CenarioLocacoesSql.Agora.AddDays(2);
    }

    private sealed class FalhaSinteticaException() : Exception("Falha sintética antes do commit.");

    private sealed class FalhaAntesLote(string tabela) : DbCommandInterceptor
    {
        public int Disparos { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains($"INSERT INTO [{tabela}]", StringComparison.Ordinal)
                || command.CommandText.Contains($"MERGE [{tabela}]", StringComparison.Ordinal)
                || tabela == "Alugueis" && command.CommandText.Contains("UPDATE [Alugueis]", StringComparison.Ordinal))
            {
                Disparos++;
                throw new FalhaSinteticaException();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FalhaDepoisSave(int chamada) : SaveChangesInterceptor
    {
        public int Chamadas { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (++Chamadas == chamada) throw new FalhaSinteticaException();
            return ValueTask.FromResult(result);
        }
    }
}
