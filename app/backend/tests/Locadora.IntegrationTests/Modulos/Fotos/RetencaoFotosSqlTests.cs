using System.Runtime.Versioning;
using System.Text.Json;
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

[TestClass, SupportedOSPlatform("linux")]
public sealed class RetencaoFotosSqlTests
{
    [TestMethod]
    public async Task MetadadosParcialmenteOcultosNaoAutorizamExclusao()
    {
        await using var e = await Ensaio.CriarAsync();
        await e.EnviarAsync();
        await e.RemoverAsync();
        var foto = await e.FotoAsync();
        await e.C.Banco.Database.OpenConnectionAsync();
        await e.C.Banco.Database.ExecuteSqlRawAsync("CREATE USER retencao_sem_metadados WITHOUT LOGIN");
        var impersonado = false;
        try
        {
            await e.C.Banco.Database.ExecuteSqlRawAsync("EXECUTE AS USER='retencao_sem_metadados'");
            impersonado = true;
            var metodo = typeof(RetomadaFotos).Assembly.GetType("Locadora.Infrastructure.Modulos.Fotos.GuardaReferenciasFotos")!
                .GetMethod("PermiteAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
            var permitido = (Task<bool>)metodo.Invoke(null, [e.C.Banco, foto, CancellationToken.None])!;
            Assert.IsFalse(await permitido);
            Assert.AreEqual(0, e.Objetos.Remocoes);
        }
        finally
        {
            if (impersonado) await e.C.Banco.Database.ExecuteSqlRawAsync("REVERT");
            await e.C.Banco.Database.ExecuteSqlRawAsync("DROP USER retencao_sem_metadados");
            await e.C.Banco.Database.CloseConnectionAsync();
        }
    }

    [TestMethod]
    public async Task SchemaConhecidoNaoTemDependenciasHistoricasDesconhecidas()
    {
        await using var banco = AmbienteIntegracao.CriarContexto();
        var dependencias = await banco.Database.SqlQuery<string>($"""
            SELECT CONCAT(OBJECT_NAME(referencing_id),':',o.type_desc) AS [Value]
            FROM sys.sql_expression_dependencies d JOIN sys.objects o ON o.object_id=d.referencing_id
            WHERE referenced_id=OBJECT_ID('dbo.FotosObjetos')
                AND referencing_id NOT IN (OBJECT_ID('dbo.FotosObjetos'),OBJECT_ID('dbo.TrabalhosObjetos'),OBJECT_ID('dbo.Veiculos'))
                AND referencing_id NOT IN (SELECT object_id FROM sys.foreign_keys
                    WHERE referenced_object_id=OBJECT_ID('dbo.FotosObjetos'))
                AND referencing_id NOT IN (SELECT object_id FROM sys.check_constraints
                    WHERE parent_object_id=OBJECT_ID('dbo.FotosObjetos') AND is_disabled=0 AND is_not_trusted=0
                        AND name IN ('CK_FotosObjetos_Estado','CK_FotosObjetos_Retencao','CK_FotosObjetos_Imagem','CK_FotosObjetos_Instantes'))
            """).ToListAsync();
        Assert.AreEqual(0, dependencias.Count, string.Join(",", dependencias));
    }

    [TestMethod]
    public async Task AtivaAntigaProtegidaEApenasInativaNoLimiteExatoPerdeBytesNaoCatalogo()
    {
        await using var e = await Ensaio.CriarAsync();
        await e.EnviarAsync();
        e.Tempo.Agora = e.Tempo.Agora.AddDays(90);
        await e.RetomarAsync();
        Assert.AreEqual(0, e.Objetos.Remocoes);
        await e.RemoverAsync();
        var foto = await e.FotoAsync();
        var piso = foto.ExcluirAposUtc!.Value;
        Assert.AreEqual(e.Tempo.Agora.AddDays(30), piso);
        e.Tempo.Agora = piso.AddTicks(-1);
        await e.RetomarAsync();
        Assert.AreEqual(0, e.Objetos.Remocoes);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await e.Volume.LerAsync(CasosFotos.Objeto(foto), default));
        e.Tempo.Agora = piso;
        await e.RetomarAsync();
        Assert.AreEqual(1, e.Objetos.Remocoes);
        Assert.IsTrue(await e.Volume.ConfirmarAusenciaAsync(CasosFotos.Objeto(foto), default));
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, (await e.TrabalhoAsync()).Estado);
        Assert.AreEqual(piso, (await e.FotoAsync()).ExcluirAposUtc);
        e.Tempo.Agora = piso.AddTicks(1);
        await e.RetomarAsync();
        Assert.AreEqual(1, e.Objetos.Remocoes);
        Assert.AreEqual(1, await e.C.Banco.FotosObjetos.CountAsync());
    }

    [TestMethod]
    public async Task SeisFalhasDeletePerdidoEHeadInconclusivoRetomamAposReinicioSemPerderAck()
    {
        await using var e = await Ensaio.CriarAsync();
        await e.EnviarAsync();
        await e.RemoverAsync();
        e.Tempo.Agora = (await e.FotoAsync()).ExcluirAposUtc!.Value;
        e.Objetos.Falhar = true;
        for (var i = 1; i <= 6; i++)
        {
            await e.RetomarAsync();
            var trabalho = await e.TrabalhoAsync();
            Assert.AreEqual(i, trabalho.Tentativas);
            Assert.IsTrue(trabalho.EnvioConfirmado);
            Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, trabalho.Estado);
            e.Tempo.Agora = trabalho.ProximoProcessamentoUtc;
        }
        e.Objetos.Falhar = false;
        e.Objetos.PerderResposta = true;
        await e.RetomarAsync();
        Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, (await e.TrabalhoAsync()).Estado);
        Assert.IsTrue(await e.Volume.ConfirmarAusenciaAsync(CasosFotos.Objeto(await e.FotoAsync()), default));
        e.Objetos.PerderResposta = false;
        e.Objetos.AusenciaInconclusiva = true;
        e.Tempo.Agora = (await e.TrabalhoAsync()).ProximoProcessamentoUtc;
        await e.RetomarAsync();
        Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, (await e.TrabalhoAsync()).Estado);
        e.Objetos.AusenciaInconclusiva = false;
        e.Tempo.Agora = (await e.TrabalhoAsync()).ProximoProcessamentoUtc;
        await e.RetomarAsync();
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, (await e.TrabalhoAsync()).Estado);
        Assert.AreEqual(8, (await e.TrabalhoAsync()).Tentativas);
        Assert.IsNotNull((await e.TrabalhoAsync()).UltimaFalhaEmUtc);
        Assert.IsNotNull((await e.TrabalhoAsync()).CodigoFalha);
    }

    [TestMethod]
    public async Task DuasRetomadasRetidasNaoCompartilhamLease()
    {
        await using var e = await Ensaio.CriarAsync();
        await e.EnviarAsync();
        await e.RemoverAsync();
        e.Tempo.Agora = (await e.FotoAsync()).ExcluirAposUtc!.Value;
        var entrou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        e.Objetos.AntesRemover = async () => { entrou.TrySetResult(); await liberar.Task.WaitAsync(TimeSpan.FromSeconds(20)); };
        var primeira = e.RetomarAsync();
        await entrou.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try { await e.RetomarAsync(); }
        finally { liberar.TrySetResult(); }
        await primeira;
        Assert.AreEqual(1, e.Objetos.Remocoes);
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, (await e.TrabalhoAsync()).Estado);
    }

    [TestMethod]
    public async Task ReferenciaDeExcluidoEFkHistoricaOuColunaDesconhecidaBloqueiamDelete()
    {
        await using var e = await Ensaio.CriarAsync();
        await e.EnviarAsync();
        await e.RemoverAsync();
        var foto = await e.FotoAsync();
        e.Tempo.Agora = foto.ExcluirAposUtc!.Value;
        await e.C.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Veiculos SET FotoAtivaId={foto.Id},ExcluidoEmUtc={e.Tempo.Agora},AlteradoEmUtc={e.Tempo.Agora} WHERE Id={foto.VeiculoId}");
        await e.RetomarAsync();
        Assert.AreEqual(0, e.Objetos.Remocoes);
        e.Tempo.Agora = (await e.TrabalhoAsync()).ProximoProcessamentoUtc;
        await e.C.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Veiculos SET FotoAtivaId=NULL WHERE Id={foto.VeiculoId}");
        await e.C.Banco.Database.ExecuteSqlRawAsync("""
            CREATE TABLE dbo.HistoricoSinteticoRetencao (Id uniqueidentifier NOT NULL PRIMARY KEY,
                FotoId uniqueidentifier NULL REFERENCES dbo.FotosObjetos(Id));
            """);
        try
        {
            await e.C.Banco.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT dbo.HistoricoSinteticoRetencao (Id,FotoId) VALUES ({Guid.NewGuid()},{foto.Id})");
            await e.RetomarAsync();
            Assert.AreEqual(0, e.Objetos.Remocoes);
            e.Tempo.Agora = (await e.TrabalhoAsync()).ProximoProcessamentoUtc;
        }
        finally { await e.C.Banco.Database.ExecuteSqlRawAsync("DROP TABLE dbo.HistoricoSinteticoRetencao"); }
        await e.C.Banco.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Veiculos ADD DependenciaFotoSintetica uniqueidentifier NULL");
        try
        {
            await e.RetomarAsync();
            Assert.AreEqual(0, e.Objetos.Remocoes);
            e.Tempo.Agora = (await e.TrabalhoAsync()).ProximoProcessamentoUtc;
        }
        finally { await e.C.Banco.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Veiculos DROP COLUMN DependenciaFotoSintetica"); }
        await e.C.Banco.Database.ExecuteSqlRawAsync("CREATE VIEW dbo.ReferenciaSinteticaRetencao AS SELECT Id FROM dbo.FotosObjetos");
        try
        {
            await e.RetomarAsync();
            Assert.AreEqual(0, e.Objetos.Remocoes);
            e.Tempo.Agora = (await e.TrabalhoAsync()).ProximoProcessamentoUtc;
        }
        finally { await e.C.Banco.Database.ExecuteSqlRawAsync("DROP VIEW dbo.ReferenciaSinteticaRetencao"); }
        await e.RetomarAsync();
        Assert.AreEqual(1, e.Objetos.Remocoes);
    }

    [TestMethod]
    public async Task LeaseVencidoDuranteIoNaoConcluiEOutraPassagemReconciliaAusencia()
    {
        await using var e = await Ensaio.CriarAsync();
        await e.EnviarAsync();
        await e.RemoverAsync();
        e.Tempo.Agora = (await e.FotoAsync()).ExcluirAposUtc!.Value;
        e.Objetos.AntesRemover = () => { e.Tempo.Agora = e.Tempo.Agora.AddMinutes(3); return Task.CompletedTask; };
        await e.RetomarAsync();
        Assert.AreEqual(EstadoTrabalhoObjeto.EmExecucao, (await e.TrabalhoAsync()).Estado);
        e.Objetos.AntesRemover = null;
        await e.RetomarAsync();
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, (await e.TrabalhoAsync()).Estado);
        Assert.AreEqual(2, e.Objetos.Remocoes);
    }

    [TestMethod]
    public async Task ExpurgoPreservaSnapshotsDemonstrativosEContratoInteiroComRowversions()
    {
        await using var e = await Ensaio.CriarAsync();
        await e.EnviarAsync();
        var aluguel = await PersistenciaDevolucaoSqlTests.CriarAbertoAsync(e.C);
        await PersistenciaDevolucaoSqlTests.FecharAsync(e.C, aluguel.Id,
            await PersistenciaDevolucaoSqlTests.PrepararDadosAsync(e.C));
        e.C.Banco.ChangeTracker.Clear();
        var original = JsonSerializer.Serialize(await PersistenciaDevolucaoSqlTests.ContratosComFinal(e.C.Banco).AsNoTracking().SingleAsync());
        await e.RemoverAsync();
        e.Tempo.Agora = (await e.FotoAsync()).ExcluirAposUtc!.Value;
        await e.RetomarAsync();
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, (await e.TrabalhoAsync()).Estado);
        var posterior = JsonSerializer.Serialize(await PersistenciaDevolucaoSqlTests.ContratosComFinal(e.C.Banco).AsNoTracking().SingleAsync());
        Assert.AreEqual(original, posterior);
        Assert.IsFalse(typeof(Domain.Modulos.Locacoes.VeiculoContratado).GetProperties()
            .Any(p => p.Name.Contains("Foto", StringComparison.OrdinalIgnoreCase)));
        var tiposDto = typeof(Api.Modulos.Locacoes.AlugueisController).Assembly.GetTypes()
            .Where(t => t.Namespace == "Locadora.Api.Modulos.Locacoes");
        Assert.IsFalse(tiposDto.SelectMany(t => t.GetProperties()).Any(p => p.Name.Contains("Foto", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(await e.Volume.ConfirmarAusenciaAsync(CasosFotos.Objeto(await e.FotoAsync()), default));
    }

    [TestMethod]
    public async Task ContadorMaximoNaoTransbordaEOutroDestinoNaoRecebeDelete()
    {
        await using var e = await Ensaio.CriarAsync();
        await e.EnviarAsync();
        await e.RemoverAsync();
        var foto = await e.FotoAsync();
        e.Tempo.Agora = foto.ExcluirAposUtc!.Value;
        await e.C.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE FotosObjetos SET Bucket='outro-destino-sintetico' WHERE Id={foto.Id}");
        await e.RetomarAsync();
        Assert.AreEqual(0, e.Objetos.Remocoes);
        await e.C.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE FotosObjetos SET Bucket={e.Destino.Bucket} WHERE Id={foto.Id}");
        await e.C.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE TrabalhosObjetos SET Tentativas={int.MaxValue} WHERE FotoObjetoId={foto.Id}");
        e.Objetos.Falhar = true;
        await e.RetomarAsync();
        var trabalho = await e.TrabalhoAsync();
        Assert.AreEqual(int.MaxValue, trabalho.Tentativas);
        Assert.AreEqual(e.Tempo.Agora.AddDays(1), trabalho.ProximoProcessamentoUtc);
        Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, trabalho.Estado);
    }

    [TestMethod]
    public async Task PosseSubstituidaAntesDaRespostaAntigaNaoSobrescreveConclusaoNova()
    {
        await using var e = await Ensaio.CriarAsync();
        await e.EnviarAsync();
        await e.RemoverAsync();
        e.Tempo.Agora = (await e.FotoAsync()).ExcluirAposUtc!.Value;
        var entrou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var chamadas = 0;
        e.Objetos.AntesRemover = async () =>
        {
            if (Interlocked.Increment(ref chamadas) == 1)
            {
                entrou.TrySetResult();
                await liberar.Task.WaitAsync(TimeSpan.FromSeconds(20));
            }
        };
        var antiga = e.RetomarAsync();
        await entrou.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try
        {
            var tokenAntigo = (await e.TrabalhoAsync()).TokenPosse;
            e.Tempo.Agora = e.Tempo.Agora.AddMinutes(3);
            await e.RetomarAsync();
            var concluido = await e.TrabalhoAsync();
            Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, concluido.Estado);
            Assert.IsNull(concluido.TokenPosse);
            Assert.IsNotNull(tokenAntigo);
        }
        finally { liberar.TrySetResult(); }
        await antiga;
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, (await e.TrabalhoAsync()).Estado);
        Assert.AreEqual(0, (await e.TrabalhoAsync()).Tentativas);
        Assert.AreEqual(2, e.Objetos.Remocoes);
    }

    [TestMethod]
    public async Task DezReferenciasProtegidasRecebemBackoffESeguinteElegivelProgride()
    {
        await using var e = await Ensaio.CriarAsync();
        var agora = e.Tempo.Agora;
        var idsProtegidos = new List<(Guid Veiculo, Guid Foto)>();
        for (var i = 0; i < 11; i++)
        {
            var v = Domain.Modulos.FrotaPrecos.Veiculo.Novo(e.C.Contexto.EmpresaId!.Value,
                new($"FIC{i:D4}", "Sintética", "Azul", "Retenção", Domain.Modulos.FrotaPrecos.TipoCombustivel.Gasolina,
                    50, 2026, e.C.Dados.Grupo.Id), agora);
            e.C.Banco.Add(v);
            await e.C.Banco.SaveChangesAsync();
            var foto = FotoObjeto.Preparar(v, "image/png", 3, 1, 1,
                System.Security.Cryptography.SHA256.HashData(new byte[] { 1, 2, 3 }), v.RowVersion,
                ProvedorFoto.Local, e.Destino.Bucket, e.C.Contexto.ContaId!.Value, "lote", agora);
            v.DefinirFoto(foto, null, agora);
            var trabalho = TrabalhoObjeto.Criar(foto, agora);
            var remocao = agora.AddSeconds(i + 1);
            v.RemoverFoto(foto, remocao);
            trabalho.AgendarRetencao(foto, remocao);
            await using (var tx = await e.C.Banco.Database.BeginTransactionAsync())
            {
                e.C.Banco.AddRange(foto, trabalho);
                await e.C.Banco.SaveChangesAsync();
                await tx.CommitAsync();
            }
            await e.Volume.GravarAsync(CasosFotos.Objeto(foto), new byte[] { 1, 2, 3 }, default);
            if (i < 10) idsProtegidos.Add((v.Id, foto.Id));
        }
        foreach (var (veiculo, foto) in idsProtegidos)
            await e.C.Banco.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Veiculos SET FotoAtivaId={foto} WHERE Id={veiculo}");
        e.Tempo.Agora = agora.AddDays(31);
        await e.RetomarAsync();
        Assert.AreEqual(0, e.Objetos.Remocoes);
        Assert.AreEqual(10, await e.C.Banco.TrabalhosObjetos.CountAsync(t => t.CodigoFalha == "referencia_protegida"));
        await e.RetomarAsync();
        Assert.AreEqual(1, e.Objetos.Remocoes);
        Assert.AreEqual(1, await e.C.Banco.TrabalhosObjetos.CountAsync(t => t.Estado == EstadoTrabalhoObjeto.Concluido));
    }

    internal sealed class Relogio : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    internal sealed class Ensaio : IAsyncDisposable
    {
        public CenarioLocacoesSql C { get; }
        public Relogio Tempo { get; } = new();
        public ArmazenamentoLocal Volume { get; }
        public ObjetosProtegidos Objetos { get; }
        public DestinoFotos Destino { get; } = new(ProvedorFoto.Local, "retencao-" + Guid.NewGuid().ToString("N"));
        private readonly CasosFotos _casos;
        private Ensaio(CenarioLocacoesSql c, Guid sessao)
        {
            C = c;
            var raiz = Path.GetFullPath(Path.Combine("obj", "retencao-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(raiz, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Volume = new(raiz, raiz + "/webroot");
            Objetos = new(Volume, c);
            _casos = new(new RepositorioVeiculos(c.Banco), new RepositorioFotos(c.Banco), new VinculosAluguelVeiculo(c.Banco),
                new UnidadeTrabalhoEmpresa(c.Banco, c.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
                new SessaoFotos(c.Banco, c.Contexto, sessao, 1, TimeProvider.System), new Processador(), Objetos, Destino,
                new RegistroAuditoriaCadastro(c.Banco, c.Contexto, Tempo), Tempo);
        }
        public static async Task<Ensaio> CriarAsync()
        {
            var c = await CenarioLocacoesSql.CriarAsync();
            var (_, _, sessao) = await CasosFotosSqlTests.CriarAsync(c);
            return new(c, sessao.Id);
        }
        public async Task EnviarAsync()
        {
            var veiculo = await C.Banco.Veiculos.SingleAsync();
            await _casos.EnviarAsync(veiculo.Id, VersaoCadastro.Etag(veiculo.RowVersion), Stream.Null, "image/png", default);
        }
        public async Task RemoverAsync()
        {
            var veiculo = await C.Banco.Veiculos.SingleAsync();
            await _casos.RemoverAsync(veiculo.Id, VersaoCadastro.Etag(veiculo.RowVersion), default);
        }
        public async Task<FotoObjeto> FotoAsync()
        {
            C.Banco.ChangeTracker.Clear();
            return await C.Banco.FotosObjetos.SingleAsync();
        }
        public async Task<TrabalhoObjeto> TrabalhoAsync()
        {
            C.Banco.ChangeTracker.Clear();
            return await C.Banco.TrabalhosObjetos.SingleAsync();
        }
        public async Task RetomarAsync()
        {
            var servicos = new ServiceCollection();
            servicos.AddScoped<ContextoEmpresa>();
            servicos.AddScoped(p => AmbienteIntegracao.CriarContextoComGuarda(
                p.GetRequiredService<ContextoEmpresa>(), C.Banco.Database.GetConnectionString()));
            servicos.AddSingleton(Destino);
            servicos.AddSingleton<IArmazenamentoObjeto>(Objetos);
            using var provedor = servicos.BuildServiceProvider(validateScopes: true);
            await new RetomadaFotos(provedor.GetRequiredService<IServiceScopeFactory>(), Tempo).RetomarAsync(default);
        }
        public async ValueTask DisposeAsync() { Volume.Dispose(); await C.DisposeAsync(); }
    }

    internal sealed class ObjetosProtegidos(IArmazenamentoObjeto volume, CenarioLocacoesSql c) : IArmazenamentoObjeto
    {
        public int Remocoes { get; private set; }
        public bool Falhar { get; set; }
        public bool PerderResposta { get; set; }
        public bool AusenciaInconclusiva { get; set; }
        public Func<Task>? AntesRemover { get; set; }
        public Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken token) => volume.GravarAsync(objeto, bytes, token);
        public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken token) => volume.LerAsync(objeto, token);
        public async Task RemoverAsync(ObjetoFoto objeto, CancellationToken token)
        {
            Assert.IsNull(c.Banco.Database.CurrentTransaction);
            Remocoes++;
            if (AntesRemover is not null) await AntesRemover();
            if (Falhar) throw new ArmazenamentoObjetoException();
            await volume.RemoverAsync(objeto, token);
            if (PerderResposta) throw new ArmazenamentoObjetoException();
        }
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken token) =>
            AusenciaInconclusiva ? Task.FromResult(false) : volume.ConfirmarAusenciaAsync(objeto, token);
    }
    private sealed class Processador : IProcessadorImagem
    {
        public Task<ImagemSaneada> ProcessarAsync(Stream arquivo, string mime, CancellationToken token) =>
            Task.FromResult(new ImagemSaneada([1, 2, 3], "image/png", 1, 1));
    }
}
