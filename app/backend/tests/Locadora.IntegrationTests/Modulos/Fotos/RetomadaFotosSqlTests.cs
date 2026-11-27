using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Modulos.Locacoes;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Locadora.Application.Modulos.Transversal;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
public sealed class RetomadaFotosSqlTests
{
    private static ServiceProvider Escopos(CenarioLocacoesSql c, CasosFotosSqlTests.ObjetosControlados objetos)
    {
        var servicos = new ServiceCollection();
        servicos.AddScoped<ContextoEmpresa>();
        servicos.AddScoped(p => AmbienteIntegracao.CriarContextoComGuarda(
            p.GetRequiredService<ContextoEmpresa>(), c.Banco.Database.GetConnectionString()));
        servicos.AddSingleton(new DestinoFotos(ProvedorFoto.Local, objetos.Bucket));
        servicos.AddSingleton<IArmazenamentoObjeto>(objetos);
        return servicos.BuildServiceProvider(validateScopes: true);
    }

    private static async Task VencerAsync(CenarioLocacoesSql c)
    {
        await c.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE TrabalhosObjetos SET LeaseAteUtc=CASE WHEN Estado=1 THEN {DateTimeOffset.UtcNow.AddMinutes(-1)} ELSE NULL END, ProximoProcessamentoUtc={DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE EmpresaId={c.Contexto.EmpresaId} AND Tipo=0");
        c.Banco.ChangeTracker.Clear();
    }

    [TestMethod]
    public async Task ReinicioCompensaSomenteNuncaAtivoComEnvioConfirmado()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, objetos, _) = await CasosFotosSqlTests.CriarAsync(c);
        var atual = await casos.EnviarAsync(c.Dados.Veiculo.Id,
            Locadora.Application.Modulos.Transversal.VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion), Stream.Null, "image/png", default);
        var pendente = await casos.PrepararAsync(atual.Id,
            Locadora.Application.Modulos.Transversal.VersaoCadastro.Etag(atual.RowVersion), new([1], "image/png", 1, 1), default);
        objetos.Dados[pendente.Objeto.Chave] = [1];
        await using (var tx = await c.Banco.Database.BeginTransactionAsync())
        {
            var trabalho = await c.Banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == pendente.FotoId);
            trabalho.ConfirmarEnvio(pendente.TokenPosse, DateTimeOffset.UtcNow);
            await c.Banco.SaveChangesAsync();
            await tx.CommitAsync();
        }
        await VencerAsync(c);
        using var servicos = Escopos(c, objetos);
        var retomada = new RetomadaFotos(servicos.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
        await new RetomadaFotosStartup(retomada, NullLogger<RetomadaFotosStartup>.Instance).StartAsync(default);
        c.Banco.ChangeTracker.Clear();
        Assert.AreEqual(EstadoFoto.Abandonada, (await c.Banco.FotosObjetos.SingleAsync(f => f.Id == pendente.FotoId)).Estado);
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido,
            (await c.Banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == pendente.FotoId)).Estado);
        Assert.AreEqual(atual.FotoAtivaId, (await c.Banco.Veiculos.SingleAsync()).FotoAtivaId);
        Assert.AreEqual(1, objetos.Dados.Count);
        Assert.AreEqual(1, objetos.Remocoes);
        await retomada.RetomarAsync(default);
        Assert.AreEqual(1, objetos.Remocoes); // Concluído/retido não entram na nova passagem.
    }

    [TestMethod]
    public async Task PutTardioMantemTombstoneBackoffERetriesSemDeleteOuAckInventado()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, objetos, _) = await CasosFotosSqlTests.CriarAsync(c);
        var pendente = await casos.PrepararAsync(c.Dados.Veiculo.Id,
            Locadora.Application.Modulos.Transversal.VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion),
            new([1], "image/png", 1, 1), default);
        using var servicos = Escopos(c, objetos);
        var retomada = new RetomadaFotos(servicos.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
        for (var tentativa = 1; tentativa <= 7; tentativa++)
        {
            await VencerAsync(c);
            // Simula escrita tardia mesmo depois de uma remoção prévia.
            objetos.Dados[pendente.Objeto.Chave] = [1];
            await retomada.RetomarAsync(default);
            c.Banco.ChangeTracker.Clear();
            var trabalho = await c.Banco.TrabalhosObjetos.SingleAsync();
            Assert.AreEqual(tentativa, trabalho.Tentativas);
            Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, trabalho.Estado);
            Assert.IsTrue(trabalho.ProximoProcessamentoUtc > DateTimeOffset.UtcNow);
            Assert.AreEqual(EstadoFoto.Abandonada, (await c.Banco.FotosObjetos.SingleAsync()).Estado);
        }
        await retomada.RetomarAsync(default);
        Assert.AreEqual(0, objetos.Remocoes);
        Assert.AreEqual(1, objetos.Dados.Count);
        Assert.AreEqual(1, await c.Banco.TrabalhosObjetos.CountAsync());
        Assert.IsNull((await c.Banco.Veiculos.SingleAsync()).FotoAtivaId);
    }

    [TestMethod]
    public async Task DuasRetomadasNaoCompartilhamPosseELeaseVivoNaoECompensado()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, objetos, _) = await CasosFotosSqlTests.CriarAsync(c);
        var pendencia = await casos.PrepararAsync(c.Dados.Veiculo.Id,
            Locadora.Application.Modulos.Transversal.VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion),
            new([1], "image/png", 1, 1), default);
        await using (var tx = await c.Banco.Database.BeginTransactionAsync())
        {
            var trabalho = await c.Banco.TrabalhosObjetos.SingleAsync();
            trabalho.ConfirmarEnvio(pendencia.TokenPosse, DateTimeOffset.UtcNow);
            await c.Banco.SaveChangesAsync();
            await tx.CommitAsync();
        }
        using var servicos = Escopos(c, objetos);
        var factory = servicos.GetRequiredService<IServiceScopeFactory>();
        var primeiro = new RetomadaFotos(factory, TimeProvider.System);
        await primeiro.RetomarAsync(default);
        Assert.AreEqual(0, objetos.Remocoes);
        await VencerAsync(c);
        var entrou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        objetos.AntesRemover = async () => { entrou.TrySetResult(); await liberar.Task.WaitAsync(TimeSpan.FromSeconds(20)); };
        var tarefa = primeiro.RetomarAsync(default);
        await entrou.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await new RetomadaFotos(factory, TimeProvider.System).RetomarAsync(default);
        liberar.TrySetResult();
        await tarefa;
        Assert.AreEqual(1, objetos.Remocoes);
    }

    [TestMethod]
    public async Task PassagemLimitadaADezEFalhaStorageNaoPerdeTrabalho()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, objetos, _) = await CasosFotosSqlTests.CriarAsync(c);
        for (var i = 0; i < 11; i++)
            await casos.PrepararAsync(c.Dados.Veiculo.Id,
                Locadora.Application.Modulos.Transversal.VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion),
                new([1], "image/png", 1, 1), default);
        await VencerAsync(c);
        objetos.FalharRemocao = true;
        using var servicos = Escopos(c, objetos);
        await new RetomadaFotos(servicos.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System).RetomarAsync(default);
        c.Banco.ChangeTracker.Clear();
        Assert.AreEqual(0, objetos.Remocoes); // Sem ack não há operação física.
        Assert.AreEqual(10, await c.Banco.TrabalhosObjetos.CountAsync(t => t.Tentativas == 1));
        Assert.AreEqual(1, await c.Banco.TrabalhosObjetos.CountAsync(t => t.Tentativas == 0));
        Assert.AreEqual(11, await c.Banco.TrabalhosObjetos.CountAsync());
    }

    [TestMethod]
    public async Task FronteirasDeFalhaECommitIncertoNuncaApagamReferenciaAtiva()
    {
        foreach (var fronteira in new[] { "pendencia", "put", "confirmacao", "leitura", "ativacao" })
        {
            await using var c = await CenarioLocacoesSql.CriarAsync();
            var (casos, objetos, sessao) = await CasosFotosSqlTests.CriarAsync(c);
            var anterior = await casos.EnviarAsync(c.Dados.Veiculo.Id,
                VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion), Stream.Null, "image/png", default);
            var idAnterior = anterior.FotoAtivaId;
            var versao = VersaoCadastro.Etag(anterior.RowVersion);
            var unidade = new UnidadeCommitIncerto(c, fronteira switch
            { "pendencia" => 2, "confirmacao" => 3, "ativacao" => 4, _ => 0 });
            if (fronteira == "put") objetos.AposGravar = () => throw new ArmazenamentoObjetoException();
            if (fronteira == "leitura") objetos.FalharLeitura = true;
            casos = CasosFotosSqlTests.Montar(c,
                new SessaoFotos(c.Banco, c.Contexto, sessao.Id, 1, TimeProvider.System), objetos, unidade: unidade);
            Exception? erro = null;
            try { await casos.EnviarAsync(c.Dados.Veiculo.Id, versao, Stream.Null, "image/png", default); }
            catch (Exception e) { erro = e; }
            Assert.IsNotNull(erro, fronteira);
            c.Banco.ChangeTracker.Clear();
            var atual = await c.Banco.Veiculos.SingleAsync();
            if (fronteira == "ativacao") Assert.AreNotEqual(idAnterior, atual.FotoAtivaId);
            else Assert.AreEqual(idAnterior, atual.FotoAtivaId);
            var fotoAtual = await c.Banco.FotosObjetos.SingleAsync(f => f.Id == atual.FotoAtivaId);
            var chaveAtual = fotoAtual.Chave;
            await VencerAsync(c);
            using var servicos = Escopos(c, objetos);
            await new RetomadaFotos(servicos.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System).RetomarAsync(default);
            Assert.IsTrue(objetos.Dados.ContainsKey(chaveAtual), fronteira);
            Assert.IsTrue(objetos.Dados.ContainsKey((await c.Banco.FotosObjetos.SingleAsync(f => f.Id == idAnterior)).Chave), fronteira);
            c.Banco.ChangeTracker.Clear();
            Assert.AreEqual(atual.FotoAtivaId, (await c.Banco.Veiculos.SingleAsync()).FotoAtivaId);
        }
    }

    [TestMethod]
    public async Task OperacaoSeguinteExecutaUmaPassagemSemLoopEStartupNaoDerrubaHost()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (_, objetos, sessao) = await CasosFotosSqlTests.CriarAsync(c);
        var retomada = new RetomadaControlada();
        var casos = CasosFotosSqlTests.Montar(c,
            new SessaoFotos(c.Banco, c.Contexto, sessao.Id, 1, TimeProvider.System), objetos, retomada: retomada);
        await casos.RemoverAsync(c.Dados.Veiculo.Id, VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion), default);
        Assert.AreEqual(1, retomada.Passagens);
        await casos.EnviarAsync(c.Dados.Veiculo.Id, VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion), Stream.Null, "image/png", default);
        Assert.AreEqual(2, retomada.Passagens);
        retomada.Falhar = true;
        await new RetomadaFotosStartup(retomada, NullLogger<RetomadaFotosStartup>.Instance).StartAsync(default);
        Assert.AreEqual(3, retomada.Passagens);
    }

    [TestMethod]
    public async Task RespostaDeDeletePerdidaPreservaAckERetomaIdempotenteAposReinicio()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, objetos, _) = await CasosFotosSqlTests.CriarAsync(c);
        var pendencia = await casos.PrepararAsync(c.Dados.Veiculo.Id,
            VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion), new([1], "image/png", 1, 1), default);
        objetos.Dados[pendencia.Objeto.Chave] = [1];
        await using (var tx = await c.Banco.Database.BeginTransactionAsync())
        {
            var trabalho = await c.Banco.TrabalhosObjetos.SingleAsync();
            trabalho.ConfirmarEnvio(pendencia.TokenPosse, DateTimeOffset.UtcNow);
            await c.Banco.SaveChangesAsync();
            await tx.CommitAsync();
        }
        await VencerAsync(c);
        objetos.AntesRemover = () =>
        {
            objetos.Dados.Remove(pendencia.Objeto.Chave);
            throw new ArmazenamentoObjetoException(); // DELETE ocorreu, resposta não chegou.
        };
        using var servicos = Escopos(c, objetos);
        var factory = servicos.GetRequiredService<IServiceScopeFactory>();
        await new RetomadaFotos(factory, TimeProvider.System).RetomarAsync(default);
        c.Banco.ChangeTracker.Clear();
        var falhou = await c.Banco.TrabalhosObjetos.SingleAsync();
        Assert.AreEqual(1, falhou.Tentativas);
        Assert.IsTrue(falhou.EnvioConfirmado);
        Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, falhou.Estado);
        objetos.AntesRemover = null;
        await VencerAsync(c);
        await new RetomadaFotos(factory, TimeProvider.System).RetomarAsync(default);
        c.Banco.ChangeTracker.Clear();
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, (await c.Banco.TrabalhosObjetos.SingleAsync()).Estado);
        Assert.AreEqual(2, objetos.Remocoes);
    }

    private sealed class RetomadaControlada : IRetomadaFotos
    {
        public int Passagens { get; private set; }
        public bool Falhar { get; set; }
        public Task RetomarAsync(CancellationToken cancelamento)
        {
            Passagens++;
            if (Falhar) throw new ArmazenamentoObjetoException();
            return Task.CompletedTask;
        }
    }

    private sealed class UnidadeCommitIncerto(CenarioLocacoesSql c, int falharApos) : IUnidadeTrabalhoEmpresa
    {
        private int chamadas;
        public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento)
        {
            var unidade = new UnidadeTrabalhoEmpresa(c.Banco, c.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
            var resultado = await unidade.ExecutarAsync(comando, cancelamento);
            if (++chamadas == falharApos) throw new IOException("Resposta sintética perdida após commit.");
            return resultado;
        }
    }
}
