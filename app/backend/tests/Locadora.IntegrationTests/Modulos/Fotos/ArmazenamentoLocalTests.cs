using System.Security.Cryptography;
using System.Runtime.Versioning;
using Locadora.Application.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
[SupportedOSPlatform("linux")]
public sealed class ArmazenamentoLocalTests
{
    [TestMethod]
    public async Task PrazoInternoViraIndisponibilidadeEPreservaObjetoSemAbsorverCancelamentoExterno()
    {
        if (!OperatingSystem.IsLinux()) Assert.Fail("Qualificação exige Linux.");
        var raiz = Path.Combine(Path.GetTempPath(), "fotos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            byte[] bytes = [1, 2, 3, 4];
            var objeto = new ObjetoFoto($"{Guid.NewGuid():N}/{Guid.NewGuid():N}", "image/png", bytes.Length, SHA256.HashData(bytes));
            using var normal = new ArmazenamentoLocal(raiz, raiz + "/webroot");
            await normal.GravarAsync(objeto, bytes, default);
            using var expirado = new ArmazenamentoLocal(raiz, raiz + "/webroot", new TempoExpirado());
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => expirado.GravarAsync(objeto, bytes, default));
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => expirado.LerAsync(objeto, default));
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => expirado.GravarAsync(objeto, bytes, new(true)));
            await Assert.ThrowsAsync<OperationCanceledException>(() => expirado.LerAsync(objeto, new(true)));
            CollectionAssert.AreEqual(bytes, await normal.LerAsync(objeto, default));
            Assert.AreEqual(1, Directory.GetFiles(Path.Combine(raiz, objeto.Chave.Split('/')[0])).Length);
        }
        finally { Directory.Delete(raiz, recursive: true); }
    }

    private sealed class TempoExpirado : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.AreEqual(TimeSpan.FromSeconds(30), dueTime);
            callback(state);
            return new Temporizador();
        }

        private sealed class Temporizador : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    [TestMethod]
    public async Task VolumePrivadoConfereIntegridadeEscritaAtomicaECancelamento()
    {
        if (!OperatingSystem.IsLinux()) Assert.Fail("Qualificação exige Linux.");
        var raiz = Path.Combine(Path.GetTempPath(), "fotos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            using var volume = new ArmazenamentoLocal(raiz, Path.Combine(raiz, "webroot"));
            byte[] bytes = [1, 2, 3, 4];
            var objeto = new ObjetoFoto($"{Guid.NewGuid():N}/{Guid.NewGuid():N}", "image/png", bytes.Length, SHA256.HashData(bytes));
            await volume.GravarAsync(objeto, bytes, default);
            CollectionAssert.AreEqual(bytes, await volume.LerAsync(objeto, default));
            Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(raiz, objeto.Chave)));
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => volume.GravarAsync(objeto, new byte[4], default));
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => volume.GravarAsync(objeto, bytes, new(true)));
            CollectionAssert.AreEqual(bytes, await volume.LerAsync(objeto, default));
            Assert.AreEqual(1, Directory.GetFiles(Path.Combine(raiz, objeto.Chave.Split('/')[0])).Length);
            await File.WriteAllBytesAsync(Path.Combine(raiz, objeto.Chave), [4, 3, 2, 1]);
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => volume.LerAsync(objeto, default));
            await File.WriteAllBytesAsync(Path.Combine(raiz, objeto.Chave), [1]);
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => volume.LerAsync(objeto, default));
            await volume.RemoverAsync(objeto, default);
            await volume.RemoverAsync(objeto, default);
            Assert.IsTrue(await volume.ConfirmarAusenciaAsync(objeto, default));
            var temporario = Path.Combine(raiz, objeto.Chave.Split('/')[0], "." + objeto.Chave.Split('/')[1] + ".pendente");
            await File.WriteAllBytesAsync(temporario, [1, 2]); // Host interrompido antes do rename.
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => volume.GravarAsync(objeto, bytes, default));
            Assert.IsTrue(File.Exists(temporario)); // Escrita que não criou o temporário não o apaga.
            await volume.RemoverAsync(objeto, default);
            Assert.IsFalse(File.Exists(temporario));
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => volume.LerAsync(objeto, default));
        }
        finally { Directory.Delete(raiz, recursive: true); }
    }

    [TestMethod]
    public async Task NegaWebrootConfiguracaoAusenteTraversalELinksMesmoAposAbrirVolume()
    {
        if (!OperatingSystem.IsLinux()) Assert.Fail("Qualificação exige Linux.");
        var raiz = Path.Combine(Path.GetTempPath(), "fotos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            Assert.ThrowsExactly<ArmazenamentoObjetoException>(() => new ArmazenamentoLocal(raiz, raiz));
            Assert.ThrowsExactly<ArmazenamentoObjetoException>(() => new ArmazenamentoLocal(raiz + "/ausente", raiz + "/web"));
            Directory.CreateSymbolicLink(raiz + "/link", raiz);
            Assert.ThrowsExactly<ArmazenamentoObjetoException>(() => new ArmazenamentoLocal(raiz + "/link", raiz + "/web"));
            using var volume = new ArmazenamentoLocal(raiz, raiz + "/web");
            var bytes = new byte[] { 1 };
            var objeto = new ObjetoFoto($"{Guid.NewGuid():N}/{Guid.NewGuid():N}", "image/png", 1, SHA256.HashData(bytes));
            foreach (var chave in new[] { "../x", "/etc/passwd", objeto.Chave + "/x", "https://externo/x" })
                await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => volume.GravarAsync(objeto with { Chave = chave }, bytes, default));
            Directory.CreateSymbolicLink(Path.Combine(raiz, objeto.Chave.Split('/')[0]), raiz);
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => volume.GravarAsync(objeto, bytes, default));
            Directory.Delete(Path.Combine(raiz, objeto.Chave.Split('/')[0]));
            await volume.GravarAsync(objeto, bytes, default);
            File.Delete(Path.Combine(raiz, objeto.Chave));
            File.CreateSymbolicLink(Path.Combine(raiz, objeto.Chave), "/etc/passwd");
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => volume.LerAsync(objeto, default));
            await volume.RemoverAsync(objeto, default); // Remove o link, nunca seu alvo.
            Assert.IsTrue(File.Exists("/etc/passwd"));
        }
        finally { Directory.Delete(raiz, recursive: true); }
    }
}
