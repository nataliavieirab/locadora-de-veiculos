using System.Diagnostics;
using Locadora.Application.Modulos.Fotos;
using Locadora.ProtocoloFotos;

namespace Locadora.Infrastructure.Modulos.Fotos;

public sealed record ConfiguracaoProcessadorImagem(string Executavel, string Processador,
    string DiretorioCgroups, string DiretorioTemporarios);

public sealed class ProcessadorImagemIsolado(ConfiguracaoProcessadorImagem configuracao) : IProcessadorImagem
{
    // Capacidade por processo da API, não por instância de DI/empresa.
    private static readonly SemaphoreSlim Slot = new(1, 1);
    private const int Memoria = 268435456;

    public async Task<ImagemSaneada> ProcessarAsync(Stream arquivo, string mime, CancellationToken cancelamento)
    {
        if (!await Slot.WaitAsync(0, cancelamento))
            throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Indisponivel);
        string? grupo = null, temporario = null;
        Process? processo = null;
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        prazo.CancelAfter(TimeSpan.FromSeconds(10));
        var token = prazo.Token;
        try
        {
            if (mime is not ("image/jpeg" or "image/png"))
                throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Formato);
            if (!OperatingSystem.IsLinux()) throw new IOException("Isolamento indisponível.");
            ValidarCaminho(configuracao.Executavel, false);
            ValidarCaminho(configuracao.Processador, false);
            ValidarCaminho(configuracao.DiretorioCgroups, true);
            ValidarCaminho(configuracao.DiretorioTemporarios, true);
            if (!File.Exists(Path.Combine(configuracao.DiretorioCgroups, "cgroup.controllers"))
                || !File.ReadAllText(Path.Combine(configuracao.DiretorioCgroups, "cgroup.subtree_control"))
                    .Split(' ', '\n').Contains("memory"))
                throw new IOException("Delegação indisponível.");
            await RetomarTemporariosAsync(token);
            var nome = "locadora-f5-" + Guid.NewGuid().ToString("N");
            temporario = Path.Combine(configuracao.DiretorioTemporarios, nome);
            Directory.CreateDirectory(temporario, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            using (var dono = Process.GetCurrentProcess())
                await File.WriteAllTextAsync(Path.Combine(temporario, ".proprietario"),
                    $"{dono.Id}:{dono.StartTime.ToUniversalTime().Ticks}", token);
            grupo = Path.Combine(configuracao.DiretorioCgroups, nome);
            Directory.CreateDirectory(grupo);
            await File.WriteAllTextAsync(Path.Combine(grupo, "memory.max"), Memoria.ToString(), token);
            await File.WriteAllTextAsync(Path.Combine(grupo, "memory.swap.max"), "0", token);
            var inicio = new ProcessStartInfo(configuracao.Executavel)
            {
                UseShellExecute = false, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = temporario
            };
            inicio.ArgumentList.Add(configuracao.Processador);
            inicio.Environment.Clear();
            inicio.Environment["HOME"] = temporario;
            inicio.Environment["TMPDIR"] = temporario;
            inicio.Environment["DOTNET_EnableDiagnostics"] = "0";
            inicio.Environment["DOTNET_PROCESSOR_COUNT"] = "1";
            processo = Process.Start(inicio) ?? throw new IOException("Processo indisponível.");
            // O filho não lê payload nem decodifica antes do handshake.
            var pronto = new byte[4];
            await processo.StandardOutput.BaseStream.ReadExactlyAsync(pronto, token);
            if (!pronto.AsSpan().SequenceEqual(ProtocoloProcessador.Pronto)) throw new IOException("Protocolo inválido.");
            await File.WriteAllTextAsync(Path.Combine(grupo, "cgroup.procs"), processo.Id.ToString(), token);
            var membros = await File.ReadAllLinesAsync(Path.Combine(grupo, "cgroup.procs"), token);
            if (!membros.Contains(processo.Id.ToString())
                || (await File.ReadAllTextAsync(Path.Combine(grupo, "memory.max"), token)).Trim() != Memoria.ToString()
                || (await File.ReadAllTextAsync(Path.Combine(grupo, "memory.swap.max"), token)).Trim() != "0")
                throw new IOException("Isolamento não confirmado.");
            // Buffer de entrada sempre limitado; nenhuma deserialização de caminho/nome do cliente.
            using var buffer = new MemoryStream();
            var bloco = new byte[81920];
            int lidos;
            while ((lidos = await arquivo.ReadAsync(bloco, token)) != 0)
            {
                if (buffer.Length + lidos > ProtocoloProcessador.MaximoBytes)
                    throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Excesso);
                buffer.Write(bloco, 0, lidos);
            }
            if (buffer.Length == 0) throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Invalida);
            var entrada = processo.StandardInput.BaseStream;
            entrada.WriteByte(ProtocoloProcessador.Iniciar);
            entrada.WriteByte(mime == "image/png" ? (byte)2 : (byte)1);
            ProtocoloProcessador.EscreverInteiro(entrada, (int)buffer.Length);
            buffer.Position = 0;
            await buffer.CopyToAsync(entrada, token);
            await entrada.FlushAsync(token);
            processo.StandardInput.Close();
            var saida = processo.StandardOutput.BaseStream;
            var codigo = new byte[1];
            await saida.ReadExactlyAsync(codigo, token);
            if (codigo[0] != ProtocoloProcessador.Sucesso)
                throw new ProcessamentoImagemException(codigo[0] switch
                {
                    ProtocoloProcessador.Invalida => FalhaProcessamentoImagem.Invalida,
                    ProtocoloProcessador.Formato => FalhaProcessamentoImagem.Formato,
                    ProtocoloProcessador.Excesso => FalhaProcessamentoImagem.Excesso,
                    _ => FalhaProcessamentoImagem.Indisponivel
                });
            await saida.ReadExactlyAsync(codigo, token);
            if (codigo[0] != (mime == "image/png" ? 2 : 1)) throw new IOException("MIME inválido.");
            var largura = await ProtocoloProcessador.LerInteiroAsync(saida, token);
            var altura = await ProtocoloProcessador.LerInteiroAsync(saida, token);
            var tamanho = await ProtocoloProcessador.LerInteiroAsync(saida, token);
            if (largura <= 0 || altura <= 0 || (long)largura * altura > ProtocoloProcessador.MaximoPixels
                || tamanho is <= 0 or > ProtocoloProcessador.MaximoBytes)
                throw new IOException("Saída inválida.");
            var bytes = new byte[tamanho];
            await saida.ReadExactlyAsync(bytes, token);
            if (await saida.ReadAsync(codigo, token) != 0) throw new IOException("Saída adicional.");
            await processo.WaitForExitAsync(token);
            if (processo.ExitCode != 0) throw new IOException("Falha do processador.");
            return new(bytes, mime, largura, altura);
        }
        catch (OperationCanceledException) when (cancelamento.IsCancellationRequested) { throw; }
        catch (ProcessamentoImagemException) { throw; }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException or InvalidOperationException
            or OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Indisponivel);
        }
        finally
        {
            try
            {
                // cgroup.kill alcança descendentes/órfãos; Kill(tree) cobre falha anterior ao ingresso.
                if (grupo is not null && Directory.Exists(grupo))
                {
                    try { File.WriteAllText(Path.Combine(grupo, "cgroup.kill"), "1"); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                if (processo is not null)
                {
                    try
                    {
                        if (!processo.HasExited) processo.Kill(entireProcessTree: true);
                        await processo.WaitForExitAsync(CancellationToken.None);
                    }
                    finally { processo.Dispose(); }
                }
                if (grupo is not null) await RemoverGrupoVazioAsync(grupo);
                if (temporario is not null && Directory.Exists(temporario))
                    Directory.Delete(temporario, recursive: true);
            }
            catch (Exception erro) when (erro is IOException or UnauthorizedAccessException
                or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Só diretórios com marcador próprio são retomados; nunca varrer o volume inteiro.
            }
            finally { Slot.Release(); }
        }
    }

    // Também pode ser chamado no startup. Uma passagem finita, sem timer/keep-alive.
    public async Task<int> RetomarTemporariosAsync(CancellationToken cancelamento)
    {
        ValidarCaminho(configuracao.DiretorioTemporarios, true);
        ValidarCaminho(configuracao.DiretorioCgroups, true);
        var removidos = 0;
        foreach (var caminho in Directory.EnumerateDirectories(configuracao.DiretorioTemporarios, "locadora-f5-*").Take(10))
        {
            cancelamento.ThrowIfCancellationRequested();
            var diretorio = new DirectoryInfo(caminho);
            if (diretorio.LinkTarget is not null || !Guid.TryParseExact(diretorio.Name["locadora-f5-".Length..], "N", out _))
                continue;
            var marcador = new FileInfo(Path.Combine(caminho, ".proprietario"));
            if (!marcador.Exists)
            {
                // Interrupção antes de escrever identidade: só um diretório estritamente vazio.
                if (!Directory.EnumerateFileSystemEntries(caminho).Any()) { Directory.Delete(caminho); removidos++; }
                continue;
            }
            if (marcador.LinkTarget is not null || marcador.Length > 128) continue;
            var partes = (await File.ReadAllTextAsync(marcador.FullName, cancelamento)).Split(':');
            if (partes.Length != 2 || !int.TryParse(partes[0], out var pid) || pid <= 0
                || !long.TryParse(partes[1], out var ticks) || ticks <= 0)
                continue;
            try
            {
                using var dono = Process.GetProcessById(pid);
                if (!dono.HasExited && dono.StartTime.ToUniversalTime().Ticks == ticks) continue;
            }
            catch (ArgumentException) { /* PID não existe mais. */ }
            var grupo = Path.Combine(configuracao.DiretorioCgroups, diretorio.Name);
            if (Directory.Exists(grupo))
            {
                await File.WriteAllTextAsync(Path.Combine(grupo, "cgroup.kill"), "1", cancelamento);
                await RemoverGrupoVazioAsync(grupo);
            }
            Directory.Delete(caminho, recursive: true);
            removidos++;
        }
        return removidos;
    }

    private static async Task RemoverGrupoVazioAsync(string grupo)
    {
        if (!Directory.Exists(grupo)) return;
        for (var tentativa = 0; tentativa < 20; tentativa++)
        {
            var eventos = await File.ReadAllLinesAsync(Path.Combine(grupo, "cgroup.events"));
            if (eventos.Contains("populated 0"))
            {
                Directory.Delete(grupo);
                return;
            }
            await Task.Delay(50);
        }
        throw new IOException("Grupo ainda ocupado; manter evidência para retomada.");
    }

    private static void ValidarCaminho(string caminho, bool diretorio)
    {
        if (!Path.IsPathFullyQualified(caminho) || caminho != Path.GetFullPath(caminho)
            || (diretorio ? !Directory.Exists(caminho) : !File.Exists(caminho)))
            throw new IOException("Caminho de configuração inválido.");
        // Configuração interna também não permite escape por symlink em qualquer ancestral.
        for (FileSystemInfo? item = diretorio ? new DirectoryInfo(caminho) : new FileInfo(caminho);
            item is not null; item = item is DirectoryInfo dir ? dir.Parent : ((FileInfo)item).Directory)
            if (item.LinkTarget is not null) throw new IOException("Link não permitido.");
    }
}
