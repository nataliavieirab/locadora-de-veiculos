using System.Runtime.Versioning;
using Locadora.Application.Modulos.Fotos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.IntegrationTests.Modulos.Locacoes;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
[SupportedOSPlatform("linux")]
public sealed class GateFotosLocalSqlTests
{
    private static readonly ImagemSaneada Imagem = new([1, 2, 3], "image/png", 1, 1);

    [TestMethod]
    public async Task FronteirasReabremVolumeESqlSemPerderTrabalhoOuApagarAtivoRetido()
    {
        foreach (var fronteira in new[] { "pendencia", "put", "confirmacao", "leitura", "ativacao", "edicao" })
        {
            await using var c = await CenarioLocacoesSql.CriarAsync();
            var (_, _, sessao) = await CasosFotosSqlTests.CriarAsync(c);
            var raiz = CriarRaiz();
            var destino = new DestinoFotos(ProvedorFoto.Local, "gate-" + Guid.NewGuid().ToString("N"));
            Guid anterior;
            Guid atual;
            using (var volume = new ArmazenamentoLocal(raiz, raiz + "/webroot"))
            {
                var objetos = new Fronteiras(volume, c);
                var casos = Montar(c, sessao.Id, objetos, destino);
                var primeira = await casos.EnviarAsync(c.Dados.Veiculo.Id,
                    VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion), Stream.Null, "image/png", default);
                anterior = primeira.FotoAtivaId!.Value;
                var etag = VersaoCadastro.Etag(primeira.RowVersion);
                objetos.FalharLeitura = fronteira == "leitura";
                objetos.AposGravar = fronteira switch
                {
                    "put" => () => throw new ArmazenamentoObjetoException(),
                    "edicao" => async () =>
                    {
                        await using var outro = AmbienteIntegracao.CriarContextoComGuarda(c.Contexto);
                        var unidade = new UnidadeTrabalhoEmpresa(outro, c.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
                        await unidade.ExecutarAsync(async token =>
                        {
                            var v = await outro.Veiculos.SingleAsync(token);
                            v.Alterar(new(v.Placa, v.Marca, "Azul", v.Modelo, v.Combustivel,
                                v.CapacidadeLitros, v.Ano, v.GrupoId), DateTimeOffset.UtcNow);
                            return true;
                        }, default);
                    },
                    _ => null,
                };
                var falharApos = fronteira switch { "pendencia" => 2, "confirmacao" => 3, "ativacao" => 4, _ => 0 };
                casos = Montar(c, sessao.Id, objetos, destino, new CommitIncerto(c, falharApos));
                Exception? erro = null;
                try { await casos.EnviarAsync(primeira.Id, etag, Stream.Null, "image/png", default); }
                catch (Exception e) { erro = e; }
                Assert.IsNotNull(erro, fronteira);
                Assert.IsTrue(erro is IOException or ArmazenamentoObjetoException or PrecondicaoCadastroException, fronteira);
                c.Banco.ChangeTracker.Clear();
                atual = (await c.Banco.Veiculos.SingleAsync()).FotoAtivaId!.Value;
                Assert.AreEqual(fronteira == "ativacao", atual != anterior, fronteira);
                Assert.AreEqual(2, await c.Banco.FotosObjetos.CountAsync(), fronteira);
                Assert.AreEqual(2, await c.Banco.TrabalhosObjetos.CountAsync(), fronteira);
                await ConferirRastreabilidadeAsync(c, raiz);
            }
            // Perda de instâncias/descritores: a recuperação só recebe SQL e o
            // volume reaberto. Não conserva dicionário nem pendência em memória.
            await VencerAsync(c);
            await ReiniciarAsync(c, raiz, destino);
            c.Banco.ChangeTracker.Clear();
            var ativa = await c.Banco.FotosObjetos.SingleAsync(f => f.Id == atual);
            var antiga = await c.Banco.FotosObjetos.SingleAsync(f => f.Id == anterior);
            using var reaberto = new ArmazenamentoLocal(raiz, raiz + "/webroot");
            CollectionAssert.AreEqual(Imagem.Bytes, await reaberto.LerAsync(CasosFotos.Objeto(ativa), default));
            CollectionAssert.AreEqual(Imagem.Bytes, await reaberto.LerAsync(CasosFotos.Objeto(antiga), default));
            Assert.AreEqual(atual, (await c.Banco.Veiculos.SingleAsync()).FotoAtivaId);
            var trabalho = await c.Banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId != anterior);
            var esperado = fronteira == "ativacao" ? EstadoTrabalhoObjeto.Retido
                : fronteira is "pendencia" or "put" ? EstadoTrabalhoObjeto.Pendente : EstadoTrabalhoObjeto.Concluido;
            Assert.AreEqual(esperado, trabalho.Estado, fronteira);
            await ConferirRastreabilidadeAsync(c, raiz);
            // Objetos anteriormente ativos permanecem no volume sintético ignorado.
        }
    }

    [TestMethod]
    public async Task PutRealTardioAposAusenciaMantemTombstoneECompensaNaPassagemSeguinte()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (_, _, sessao) = await CasosFotosSqlTests.CriarAsync(c);
        var raiz = CriarRaiz();
        var destino = new DestinoFotos(ProvedorFoto.Local, "gate-" + Guid.NewGuid().ToString("N"));
        using var volume = new ArmazenamentoLocal(raiz, raiz + "/webroot");
        var casos = Montar(c, sessao.Id, volume, destino);
        var pendencia = await casos.PrepararAsync(c.Dados.Veiculo.Id,
            VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion), Imagem, default);
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var escrita = Task.Run(async () =>
        {
            await liberar.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await volume.GravarAsync(pendencia.Objeto, Imagem.Bytes, default);
        });
        try
        {
            await VencerAsync(c);
            await ReiniciarAsync(c, raiz, destino);
            c.Banco.ChangeTracker.Clear();
            var primeira = await c.Banco.TrabalhosObjetos.SingleAsync();
            Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, primeira.Estado);
            Assert.AreEqual(1, primeira.Tentativas);
            Assert.IsTrue(await volume.ConfirmarAusenciaAsync(pendencia.Objeto, default));
        }
        finally { liberar.TrySetResult(); }
        await escrita;
        CollectionAssert.AreEqual(Imagem.Bytes, await volume.LerAsync(pendencia.Objeto, default));
        await ConferirRastreabilidadeAsync(c, raiz);
        await VencerAsync(c);
        await ReiniciarAsync(c, raiz, destino);
        c.Banco.ChangeTracker.Clear();
        Assert.IsFalse(await volume.ConfirmarAusenciaAsync(pendencia.Objeto, default));
        var segunda = await c.Banco.TrabalhosObjetos.SingleAsync();
        Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, segunda.Estado); // fim do PUT não confirmado no SQL
        Assert.AreEqual(2, segunda.Tentativas);
        Assert.IsNull((await c.Banco.Veiculos.SingleAsync()).FotoAtivaId);
        Assert.AreEqual(EstadoFoto.Abandonada, (await c.Banco.FotosObjetos.SingleAsync()).Estado);
        await ConferirRastreabilidadeAsync(c, raiz);
    }

    [TestMethod]
    public async Task TemporarioInterrompidoEDeleteComRespostaPerdidaRetomamComVolumeReal()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (_, _, sessao) = await CasosFotosSqlTests.CriarAsync(c);
        var raiz = CriarRaiz();
        var destino = new DestinoFotos(ProvedorFoto.Local, "gate-" + Guid.NewGuid().ToString("N"));
        using var volume = new ArmazenamentoLocal(raiz, raiz + "/webroot");
        var casos = Montar(c, sessao.Id, volume, destino);
        var pendencia = await casos.PrepararAsync(c.Dados.Veiculo.Id,
            VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion), Imagem, default);
        var pasta = Path.Combine(raiz, pendencia.Objeto.Chave.Split('/')[0]);
        Directory.CreateDirectory(pasta, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var temporario = Path.Combine(pasta, "." + pendencia.FotoId.ToString("N") + ".pendente");
        await File.WriteAllBytesAsync(temporario, [1]); // interrupção anterior ao rename
        await VencerAsync(c);
        await ReiniciarAsync(c, raiz, destino);
        Assert.IsTrue(File.Exists(temporario)); // PUT sem ack não autoriza remoção física.
        c.Banco.ChangeTracker.Clear();
        Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, (await c.Banco.TrabalhosObjetos.SingleAsync()).Estado);

        // Outra pendência teve PUT terminado e checkpoint durável; só a resposta
        // de DELETE será perdida. Nenhuma confirmação é inventada para a primeira.
        var outra = await casos.PrepararAsync(c.Dados.Veiculo.Id,
            VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion), Imagem, default);
        await volume.GravarAsync(outra.Objeto, Imagem.Bytes, default);
        await new UnidadeTrabalhoEmpresa(c.Banco, c.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance)
            .ExecutarAsync(async token =>
            {
                var envio = await c.Banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == outra.FotoId, token);
                envio.ConfirmarEnvio(outra.TokenPosse, DateTimeOffset.UtcNow);
                return true;
            }, default);
        await VencerAsync(c);
        await ReiniciarAsync(c, raiz, destino, perderDelete: true);
        c.Banco.ChangeTracker.Clear();
        var trabalho = await c.Banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == outra.FotoId);
        Assert.IsTrue(trabalho.EnvioConfirmado);
        Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, trabalho.Estado);
        Assert.IsTrue(await volume.ConfirmarAusenciaAsync(outra.Objeto, default));
        await VencerAsync(c);
        await ReiniciarAsync(c, raiz, destino);
        c.Banco.ChangeTracker.Clear();
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido,
            (await c.Banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == outra.FotoId)).Estado);
        Assert.AreEqual(2, await c.Banco.TrabalhosObjetos.CountAsync());
        await ConferirRastreabilidadeAsync(c, raiz);
    }

    private static string CriarRaiz()
    {
        if (!OperatingSystem.IsLinux()) Assert.Fail("Gate local exige Linux.");
        var raiz = Path.GetFullPath(Path.Combine("obj", "gate-fotos-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(raiz, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return raiz;
    }

    private static CasosFotos Montar(CenarioLocacoesSql c, Guid sessao, IArmazenamentoObjeto objetos,
        DestinoFotos destino, IUnidadeTrabalhoEmpresa? unidade = null) =>
        new(new RepositorioVeiculos(c.Banco), new RepositorioFotos(c.Banco), new VinculosAluguelVeiculo(c.Banco),
            unidade ?? new UnidadeTrabalhoEmpresa(c.Banco, c.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
            new SessaoFotos(c.Banco, c.Contexto, sessao, 1, TimeProvider.System), new Processador(), objetos, destino,
            new RegistroAuditoriaCadastro(c.Banco, c.Contexto, TimeProvider.System), TimeProvider.System);

    private static async Task VencerAsync(CenarioLocacoesSql c)
    {
        await c.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE TrabalhosObjetos SET LeaseAteUtc=CASE WHEN Estado=1 THEN {DateTimeOffset.UtcNow.AddMinutes(-1)} ELSE NULL END, ProximoProcessamentoUtc={DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE EmpresaId={c.Contexto.EmpresaId} AND Tipo=0");
        c.Banco.ChangeTracker.Clear();
    }

    private static async Task ReiniciarAsync(CenarioLocacoesSql c, string raiz, DestinoFotos destino, bool perderDelete = false)
    {
        using var volume = new ArmazenamentoLocal(raiz, raiz + "/webroot");
        var servicos = new ServiceCollection();
        servicos.AddScoped<ContextoEmpresa>();
        servicos.AddScoped(p => AmbienteIntegracao.CriarContextoComGuarda(
            p.GetRequiredService<ContextoEmpresa>(), c.Banco.Database.GetConnectionString()));
        servicos.AddSingleton(destino);
        servicos.AddSingleton<IArmazenamentoObjeto>(new Fronteiras(volume, c) { PerderDelete = perderDelete });
        using var provedor = servicos.BuildServiceProvider(validateScopes: true);
        await new RetomadaFotosStartup(
            new RetomadaFotos(provedor.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System),
            NullLogger<RetomadaFotosStartup>.Instance).StartAsync(default);
    }

    private static async Task ConferirRastreabilidadeAsync(CenarioLocacoesSql c, string raiz)
    {
        var fotos = await c.Banco.FotosObjetos.AsNoTracking().ToListAsync();
        var trabalhos = await c.Banco.TrabalhosObjetos.AsNoTracking().ToListAsync();
        Assert.AreEqual(fotos.Count, trabalhos.Count);
        foreach (var foto in fotos) Assert.AreEqual(1, trabalhos.Count(t => t.FotoObjetoId == foto.Id));
        foreach (var arquivo in Directory.EnumerateFiles(raiz, "*", SearchOption.AllDirectories))
        {
            var relativo = Path.GetRelativePath(raiz, arquivo);
            Assert.IsTrue(fotos.Any(f => f.Chave == relativo
                || f.Chave.Split('/')[0] + "/." + f.Id.ToString("N") + ".pendente" == relativo),
                "Todo objeto/temporário próprio precisa de foto e trabalho duráveis.");
        }
    }

    private sealed class Processador : IProcessadorImagem
    {
        public Task<ImagemSaneada> ProcessarAsync(Stream arquivo, string mime, CancellationToken cancelamento) => Task.FromResult(Imagem);
    }

    private sealed class Fronteiras(IArmazenamentoObjeto volume, CenarioLocacoesSql c) : IArmazenamentoObjeto
    {
        public Func<Task>? AposGravar { get; set; }
        public bool FalharLeitura { get; set; }
        public bool PerderDelete { get; set; }
        public async Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken cancelamento)
        {
            Assert.IsNull(c.Banco.Database.CurrentTransaction);
            await volume.GravarAsync(objeto, bytes, cancelamento);
            if (AposGravar is not null) await AposGravar();
        }
        public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken cancelamento)
        {
            Assert.IsNull(c.Banco.Database.CurrentTransaction);
            if (FalharLeitura) throw new ArmazenamentoObjetoException();
            return volume.LerAsync(objeto, cancelamento);
        }
        public async Task RemoverAsync(ObjetoFoto objeto, CancellationToken cancelamento)
        {
            Assert.IsNull(c.Banco.Database.CurrentTransaction);
            await volume.RemoverAsync(objeto, cancelamento);
            if (PerderDelete) throw new ArmazenamentoObjetoException();
        }
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken cancelamento) =>
            volume.ConfirmarAusenciaAsync(objeto, cancelamento);
    }

    private sealed class CommitIncerto(CenarioLocacoesSql c, int falharApos) : IUnidadeTrabalhoEmpresa
    {
        private int chamadas;
        public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento)
        {
            var resultado = await new UnidadeTrabalhoEmpresa(c.Banco, c.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance)
                .ExecutarAsync(comando, cancelamento);
            if (++chamadas == falharApos) throw new IOException("Resposta sintética perdida após commit real.");
            return resultado;
        }
    }
}
