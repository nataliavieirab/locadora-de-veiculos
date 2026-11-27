using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Locadora.Application.Modulos.Fotos;
using Microsoft.Win32.SafeHandles;

namespace Locadora.Infrastructure.Modulos.Fotos;

/// <summary>Volume Linux privado. Operações relativas a descritores com O_NOFOLLOW,
/// inclusive no diretório da empresa; não seguem links mesmo durante troca concorrente.</summary>
[SupportedOSPlatform("linux")]
public sealed class ArmazenamentoLocal : IArmazenamentoObjeto, IDisposable
{
    private const int Diretorio = 0x10000, SemLink = 0x20000, CloseExec = 0x80000;
    private readonly SafeFileHandle raiz;
    private readonly TimeProvider tempo;

    public ArmazenamentoLocal(string caminho, string webroot, TimeProvider? tempo = null)
    {
        this.tempo = tempo ?? TimeProvider.System;
        if (!Path.IsPathFullyQualified(caminho) || !Path.IsPathFullyQualified(webroot))
            throw new ArmazenamentoObjetoException();
        caminho = Path.TrimEndingDirectorySeparator(Path.GetFullPath(caminho));
        webroot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(webroot));
        if (caminho == "/" || caminho == webroot || caminho.StartsWith(webroot + "/", StringComparison.Ordinal))
            throw new ArmazenamentoObjetoException();
        // A raiz deve ser provisionada, não há pasta pública/temporária de fallback.
        for (var parte = new DirectoryInfo(caminho); parte is not null; parte = parte.Parent)
            if (!parte.Exists || parte.LinkTarget is not null)
                throw new ArmazenamentoObjetoException();
        var modo = File.GetUnixFileMode(caminho);
        if ((modo & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new ArmazenamentoObjetoException();
        raiz = Abrir(-100, caminho, Diretorio);
    }

    public async Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken cancelamento)
    {
        objeto.Conferir(bytes.Span);
        cancelamento.ThrowIfCancellationRequested();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30), tempo);
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancelamento, limite.Token);
        using var pasta = AbrirEmpresa(objeto, criar: true);
        var nome = objeto.Chave.Split('/')[1];
        // Nome recuperável a partir da pendência durável, sem órfão anônimo
        // se o host cair antes do rename. O_EXCL mantém uma única escrita por chave.
        var temporario = "." + nome + ".pendente";
        var criouTemporario = false;
        try
        {
            using var handle = Abrir(pasta.DangerousGetHandle().ToInt32(), temporario, 1 | 0x40 | 0x80);
            criouTemporario = true;
            await using (var stream = new FileStream(handle, FileAccess.Write))
            {
                await stream.WriteAsync(bytes, prazo.Token);
                stream.Flush(flushToDisk: true);
            }
            prazo.Token.ThrowIfCancellationRequested();
            if (renameat(pasta.DangerousGetHandle().ToInt32(), temporario,
                pasta.DangerousGetHandle().ToInt32(), nome) != 0)
                throw new ArmazenamentoObjetoException();
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        { throw new ArmazenamentoObjetoException(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new ArmazenamentoObjetoException(); }
        finally
        {
            if (criouTemporario) _ = unlinkat(pasta.DangerousGetHandle().ToInt32(), temporario, 0);
        }
    }

    public async Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken cancelamento)
    {
        objeto.Validar();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30), tempo);
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancelamento, limite.Token);
        using var pasta = AbrirEmpresa(objeto, criar: false);
        try
        {
            using var handle = Abrir(pasta.DangerousGetHandle().ToInt32(), objeto.Chave.Split('/')[1], 0);
            await using var stream = new FileStream(handle, FileAccess.Read);
            if (stream.Length != objeto.TamanhoBytes) throw new ArmazenamentoObjetoException();
            var bytes = new byte[objeto.TamanhoBytes];
            await stream.ReadExactlyAsync(bytes, prazo.Token);
            if (await stream.ReadAsync(new byte[1], prazo.Token) != 0) throw new ArmazenamentoObjetoException();
            objeto.Conferir(bytes);
            return bytes;
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        { throw new ArmazenamentoObjetoException(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new ArmazenamentoObjetoException(); }
    }

    public Task RemoverAsync(ObjetoFoto objeto, CancellationToken cancelamento)
    {
        objeto.Validar();
        cancelamento.ThrowIfCancellationRequested();
        var empresa = objeto.Chave.Split('/')[0];
        var fd = openat(raiz.DangerousGetHandle().ToInt32(), empresa, Diretorio | SemLink | CloseExec, 0);
        if (fd < 0)
        {
            if (Marshal.GetLastPInvokeError() == 2) return Task.CompletedTask;
            throw new ArmazenamentoObjetoException();
        }
        using var pasta = new SafeFileHandle(fd, ownsHandle: true);
        if (unlinkat(fd, objeto.Chave.Split('/')[1], 0) != 0 && Marshal.GetLastPInvokeError() != 2)
            throw new ArmazenamentoObjetoException();
        if (unlinkat(fd, "." + objeto.Chave.Split('/')[1] + ".pendente", 0) != 0 && Marshal.GetLastPInvokeError() != 2)
            throw new ArmazenamentoObjetoException();
        return Task.CompletedTask;
    }

    private SafeFileHandle AbrirEmpresa(ObjetoFoto objeto, bool criar)
    {
        objeto.Validar();
        var empresa = objeto.Chave.Split('/')[0];
        var fd = raiz.DangerousGetHandle().ToInt32();
        if (criar && mkdirat(fd, empresa, 0x1C0) != 0 && Marshal.GetLastPInvokeError() != 17)
            throw new ArmazenamentoObjetoException();
        return Abrir(fd, empresa, Diretorio);
    }

    public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken cancelamento)
    {
        objeto.Validar();
        cancelamento.ThrowIfCancellationRequested();
        var empresa = objeto.Chave.Split('/')[0];
        var fd = openat(raiz.DangerousGetHandle().ToInt32(), empresa, Diretorio | SemLink | CloseExec, 0);
        if (fd < 0)
        {
            if (Marshal.GetLastPInvokeError() == 2) return Task.FromResult(true);
            throw new ArmazenamentoObjetoException();
        }
        using var pasta = new SafeFileHandle(fd, ownsHandle: true);
        var arquivo = openat(fd, objeto.Chave.Split('/')[1], SemLink | CloseExec | 0x800, 0);
        if (arquivo < 0)
        {
            if (Marshal.GetLastPInvokeError() == 2) return Task.FromResult(true);
            throw new ArmazenamentoObjetoException();
        }
        using var handle = new SafeFileHandle(arquivo, ownsHandle: true);
        return Task.FromResult(false);
    }

    private static SafeFileHandle Abrir(int fd, string nome, int flags)
    {
        var aberto = openat(fd, nome, flags | SemLink | CloseExec | 0x800, 0x180);
        if (aberto < 0) throw new ArmazenamentoObjetoException();
        return new SafeFileHandle(aberto, ownsHandle: true);
    }

    public void Dispose() => raiz.Dispose();

    [DllImport("libc", SetLastError = true)] private static extern int openat(int fd, string nome, int flags, int modo);
    [DllImport("libc", SetLastError = true)] private static extern int mkdirat(int fd, string nome, int modo);
    [DllImport("libc", SetLastError = true)] private static extern int renameat(int origem, string nome, int destino, string novoNome);
    [DllImport("libc", SetLastError = true)] private static extern int unlinkat(int fd, string nome, int flags);
}
