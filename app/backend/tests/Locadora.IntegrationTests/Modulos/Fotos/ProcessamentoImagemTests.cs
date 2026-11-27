using System.Buffers.Binary;
using System.Diagnostics;
using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;
using SkiaSharp;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
public sealed class ProcessamentoImagemTests
{
    private string _temporarios = "";
    private ConfiguracaoProcessadorImagem _configuracao = null!;

    [TestInitialize]
    public void Preparar()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Ensaio real requer Linux/cgroup v2 delegado.");
        var atual = File.ReadAllLines("/proc/self/cgroup").Single(l => l.StartsWith("0::", StringComparison.Ordinal))[3..];
        var pai = Directory.GetParent(Path.Combine("/sys/fs/cgroup", atual.TrimStart('/')))!.FullName;
        var backend = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(backend.FullName, "Locadora.slnx"))) backend = backend.Parent!;
        _temporarios = Path.Combine(backend.FullName, "tests/Locadora.IntegrationTests/obj/fotos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporarios, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var dotnet = File.ResolveLinkTarget("/usr/bin/dotnet", returnFinalTarget: true)!.FullName;
        var worker = Path.Combine(backend.FullName, "src/Locadora.ProcessadorImagens/bin/Debug/net10.0/Locadora.ProcessadorImagens.dll");
        _configuracao = new(dotnet, worker, pai, _temporarios);
    }

    [TestCleanup]
    public void Limpar()
    {
        if (Directory.Exists(_temporarios)) Directory.Delete(_temporarios, recursive: true);
    }

    private Task<ImagemSaneada> Processar(byte[] bytes, string mime = "image/png") =>
        new ProcessadorImagemIsolado(_configuracao).ProcessarAsync(new MemoryStream(bytes), mime, CancellationToken.None);

    private async Task Rejeita(byte[] bytes, FalhaProcessamentoImagem falha, string mime = "image/png")
    {
        var erro = await Assert.ThrowsExactlyAsync<ProcessamentoImagemException>(() => Processar(bytes, mime));
        Assert.AreEqual(falha, erro.Falha);
        Assert.AreEqual(0, Directory.GetDirectories(_temporarios, "locadora-f5-*").Length);
    }

    private static byte[] Imagem(int largura = 32, int altura = 24, bool jpeg = false, bool ruido = false)
    {
        using var bitmap = new SKBitmap(largura, altura);
        bitmap.Erase(SKColors.Transparent);
        if (ruido)
        {
            var pixels = new byte[largura * altura * 4];
            new Random(37).NextBytes(pixels);
            for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        }
        else
        {
            using var canvas = new SKCanvas(bitmap);
            using var tinta = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, largura / 2, altura, tinta);
        }
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 90);
        return dados.ToArray();
    }

    [TestMethod]
    public async Task PngPreservaTransparenciaESemMetadadosOuNomeOriginal()
    {
        var bytes = InserirChunk(Imagem(), "tEXt", "Autor\0SINTETICO-EXIF-NOME"u8.ToArray());
        var resultado = await Processar(bytes);
        Assert.AreEqual("image/png", resultado.Mime);
        Assert.AreEqual(32, resultado.Largura);
        Assert.IsTrue(resultado.Bytes.Length <= FotoObjeto.MaximoBytes);
        Assert.IsFalse(System.Text.Encoding.Latin1.GetString(resultado.Bytes).Contains("SINTETICO-EXIF-NOME", StringComparison.Ordinal));
        using var codec = SKCodec.Create(new MemoryStream(resultado.Bytes));
        using var bitmap = new SKBitmap(codec.Info);
        Assert.AreEqual(SKCodecResult.Success, codec.GetPixels(bitmap.Info, bitmap.GetPixels()));
        Assert.AreEqual((byte)0, bitmap.GetPixel(31, 0).Alpha);
        Assert.AreEqual(SKColors.Red, bitmap.GetPixel(0, 0));
    }

    [TestMethod]
    public async Task JpegAplicaTodasOrientacoesERemoveExif()
    {
        for (ushort orientacao = 1; orientacao <= 8; orientacao++)
        {
            var jpeg = Imagem(32, 16, jpeg: true);
            // APP1 EXIF/TIFF little endian, tag Orientation, sem dados pessoais.
            byte[] exif = [69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0, 1, 0,
                18, 1, 3, 0, 1, 0, 0, 0, (byte)orientacao, 0, 0, 0, 0, 0, 0, 0];
            byte[] entrada = [255, 216, 255, 225, 0, (byte)(exif.Length + 2), .. exif, .. jpeg[2..]];
            var resultado = await Processar(entrada, "image/jpeg");
            Assert.AreEqual(orientacao >= 5 ? 16 : 32, resultado.Largura);
            Assert.AreEqual(orientacao >= 5 ? 32 : 16, resultado.Altura);
            Assert.IsFalse(System.Text.Encoding.Latin1.GetString(resultado.Bytes).Contains("Exif", StringComparison.Ordinal));
            using var codec = SKCodec.Create(new MemoryStream(resultado.Bytes));
            Assert.AreEqual(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
            using var bitmap = new SKBitmap(codec.Info);
            Assert.AreEqual(SKCodecResult.Success, codec.GetPixels(bitmap.Info, bitmap.GetPixels()));
            var vermelhoNoTopoEsquerdo = orientacao is 1 or 4 or 5 or 6;
            Assert.AreEqual(vermelhoNoTopoEsquerdo, bitmap.GetPixel(1, 1).Red > 180, $"orientação {orientacao}");
        }
    }

    [TestMethod]
    public async Task RejeitaVazioFormatoMimeCorrompidoTruncadoAnimacaoECRC()
    {
        var png = Imagem();
        await Rejeita([], FalhaProcessamentoImagem.Invalida);
        await Rejeita("GIF89a"u8.ToArray(), FalhaProcessamentoImagem.Formato);
        await Rejeita(png, FalhaProcessamentoImagem.Formato, "image/jpeg");
        await Rejeita(png, FalhaProcessamentoImagem.Formato, "application/octet-stream");
        await Rejeita(png[..^1], FalhaProcessamentoImagem.Invalida);
        await Rejeita([.. png, 0], FalhaProcessamentoImagem.Invalida);
        var corrompida = png.ToArray(); corrompida[30] ^= 1;
        await Rejeita(corrompida, FalhaProcessamentoImagem.Invalida);
        await Rejeita(InserirChunk(png, "acTL", new byte[8]), FalhaProcessamentoImagem.Invalida);
        var jpeg = Imagem(jpeg: true);
        await Rejeita(jpeg[..^2], FalhaProcessamentoImagem.Invalida, "image/jpeg");
        await Rejeita([.. jpeg, 0], FalhaProcessamentoImagem.Invalida, "image/jpeg");
        // Estrutura completa, mas scan insuficiente: exige Success, não IncompleteInput.
        var sos = Enumerable.Range(2, jpeg.Length - 3).First(i => jpeg[i] == 255 && jpeg[i + 1] == 218);
        var inicioScan = sos + 2 + BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(sos + 2));
        await Rejeita([.. jpeg[..inicioScan], 255, 217], FalhaProcessamentoImagem.Invalida, "image/jpeg");
    }

    [TestMethod]
    public async Task DimensoesSaoLimitadasAntesDeAlocarETetoDeEntradaEhIndependente()
    {
        foreach (var (largura, altura) in new[] { (4001, 4000), (0, 1), (int.MaxValue, int.MaxValue) })
        {
            var png = Imagem();
            BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), largura);
            BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), altura);
            GravarCrc(png.AsSpan(12, 17), png.AsSpan(29, 4));
            await Rejeita(png, FalhaProcessamentoImagem.Invalida);
        }
        await Rejeita(new byte[FotoObjeto.MaximoBytes + 1], FalhaProcessamentoImagem.Excesso);
        var aceita = await Processar(Imagem(4000, 4000));
        Assert.AreEqual(4000, aceita.Largura);
        Assert.AreEqual(4000, aceita.Altura);
        // Padding em chunk ancillary válido deixa a entrada exatamente em 5 MiB.
        var pequena = Imagem();
        var metadados = new byte[FotoObjeto.MaximoBytes - pequena.Length - 12];
        Array.Fill(metadados, (byte)'x');
        metadados[0] = (byte)'A'; metadados[1] = 0; // Keyword e separador válidos do tEXt.
        var noLimite = InserirChunk(pequena, "tEXt", metadados);
        Assert.AreEqual(FotoObjeto.MaximoBytes, noLimite.Length);
        Assert.AreEqual(32, (await Processar(noLimite)).Largura);
    }

    [TestMethod]
    public async Task ReencodeQueUltrapassaCincoMiBNaoEhRedimensionadoOuAtivado()
    {
        // PNG com ruído de entrada codificado como JPEG cabe, mas JPEG não cresce
        // necessariamente. Para PNG, conteúdo altamente comprimível em formato indexed
        // pode expandir após saneamento RGBA: cria fixture indexed de 16 MP.
        var png = PngIndexado(4000, 4000);
        Assert.IsTrue(png.Length <= FotoObjeto.MaximoBytes);
        await Rejeita(png, FalhaProcessamentoImagem.Excesso);
    }

    [TestMethod]
    public async Task FalhaDeIsolamentoNaoIniciaDecodeELiberaSlot()
    {
        var real = _configuracao;
        _configuracao = real with { DiretorioCgroups = _temporarios };
        await Rejeita(Imagem(), FalhaProcessamentoImagem.Indisponivel);
        _configuracao = real;
        Assert.AreEqual(32, (await Processar(Imagem())).Largura);
    }

    [TestMethod]
    public async Task SupervisorMataExcessoDeMemoriaEArvorePorPrazoSemHerdarCredenciais()
    {
        var real = _configuracao;
        var grupoEnsaio = Path.Combine(real.DiretorioCgroups, "locadora-f5-teste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(grupoEnsaio);
        await File.WriteAllTextAsync(Path.Combine(grupoEnsaio, "cgroup.subtree_control"), "+memory");
        _configuracao = real with { DiretorioCgroups = grupoEnsaio };
        try
        {
            _configuracao = Script("""
            import os,sys
            sys.stdout.buffer.write(b'F5RD'); sys.stdout.buffer.flush()
            sys.stdin.buffer.read()
            assert not any('SQL' in k or 'AWS' in k or 'SECRET' in k or 'TOKEN' in k for k in os.environ)
            a=bytearray(300*1024*1024)
            """);
            await Rejeita(Imagem(), FalhaProcessamentoImagem.Indisponivel);
            var eventos = await File.ReadAllLinesAsync(Path.Combine(grupoEnsaio, "memory.events"));
            Assert.IsTrue(eventos.Any(linha => linha.StartsWith("oom_kill ", StringComparison.Ordinal)
                && long.Parse(linha.Split(' ')[1]) > 0));
            _configuracao = Script("""
            import os,sys,time
            sys.stdout.buffer.write(b'F5RD'); sys.stdout.buffer.flush()
            sys.stdin.buffer.read()
            pid=os.fork()
            time.sleep(60)
            """);
            var relogio = Stopwatch.StartNew();
            await Rejeita(Imagem(), FalhaProcessamentoImagem.Indisponivel);
            Assert.IsTrue(relogio.Elapsed >= TimeSpan.FromSeconds(9) && relogio.Elapsed < TimeSpan.FromSeconds(15));
            Assert.AreEqual(0, Directory.GetDirectories(grupoEnsaio).Length);
        }
        finally
        {
            _configuracao = real;
            Directory.Delete(grupoEnsaio);
        }
        Assert.AreEqual(32, (await Processar(Imagem())).Largura);
    }

    [TestMethod]
    public async Task OcupadoNaoEnfileiraECancelamentoEncerraFilhoELiberaSlot()
    {
        _configuracao = Script("""
            import sys,time
            sys.stdout.buffer.write(b'F5RD'); sys.stdout.buffer.flush()
            sys.stdin.buffer.read()
            time.sleep(60)
            """);
        using var cancelar = new CancellationTokenSource();
        var primeiro = new ProcessadorImagemIsolado(_configuracao)
            .ProcessarAsync(new MemoryStream(Imagem()), "image/png", cancelar.Token);
        try
        {
            var erro = await Assert.ThrowsExactlyAsync<ProcessamentoImagemException>(() => Processar(Imagem()));
            Assert.AreEqual(FalhaProcessamentoImagem.Indisponivel, erro.Falha);
        }
        finally { cancelar.Cancel(); }
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await primeiro);
        Assert.AreEqual(0, Directory.GetDirectories(_temporarios, "locadora-f5-*").Length);
    }

    [TestMethod]
    public async Task FilhoSoRecebeAmbienteMinimoEIsolamentoConfirmadoAntesDoPayload()
    {
        var imagem = Convert.ToBase64String(Imagem());
        _configuracao = Script("""
            import os,sys,struct,pathlib,base64
            sys.stdout.buffer.write(b'F5RD'); sys.stdout.buffer.flush()
            inicio=sys.stdin.buffer.read(1)
            assert inicio==bytes([42])
            assert not any(any(x in k.upper() for x in ['SQL','AWS','SECRET','TOKEN','CONNECTION','R2']) for k in os.environ)
            assert set(os.environ).issubset({'HOME','TMPDIR','DOTNET_EnableDiagnostics','DOTNET_PROCESSOR_COUNT','LC_CTYPE'})
            grupo=pathlib.Path('/sys/fs/cgroup')/pathlib.Path('/proc/self/cgroup').read_text().strip()[3:].lstrip('/')
            assert (grupo/'memory.max').read_text().strip()=='268435456'
            assert (grupo/'memory.swap.max').read_text().strip()=='0'
            sys.stdin.buffer.read()
            """ + $"\ndata=base64.b64decode('{imagem}')\n" + """
            sys.stdout.buffer.write(bytes([0,2])+struct.pack('<iii',32,24,len(data))+data)
            sys.stdout.buffer.flush()
            """);
        Assert.AreEqual(32, (await Processar(Imagem())).Largura);
    }

    [TestMethod]
    public async Task SupervisorRejeitaTamanhoOuBytesAdicionaisNoProtocolo()
    {
        foreach (var comando in new[]
        {
            "sys.stdout.buffer.write(bytes([0,2])+struct.pack('<iii',1,1,5242881))",
            "sys.stdout.buffer.write(bytes([0,2])+struct.pack('<iii',1,1,1)+bytes([1,2]))",
            "sys.stdout.buffer.write(bytes([99]))"
        })
        {
            _configuracao = Script("""
                import sys,struct
                sys.stdout.buffer.write(b'F5RD'); sys.stdout.buffer.flush()
                sys.stdin.buffer.read()
                """ + "\n" + comando + "\nsys.stdout.buffer.flush()\n");
            await Rejeita(Imagem(), FalhaProcessamentoImagem.Indisponivel);
        }
    }

    [TestMethod]
    public async Task RetomaTemporarioDeHostInterrompidoMataOrfaoENaoApagaDiretoriosAlheios()
    {
        var nome = "locadora-f5-" + Guid.NewGuid().ToString("N");
        var privado = Path.Combine(_temporarios, nome);
        Directory.CreateDirectory(privado);
        await File.WriteAllTextAsync(Path.Combine(privado, ".proprietario"), "2147483647:1");
        await File.WriteAllTextAsync(Path.Combine(privado, "entrada-propria"), "sintetico");
        var alheio = Path.Combine(_temporarios, "nao-pertence-ao-processador");
        Directory.CreateDirectory(alheio);
        var grupo = Path.Combine(_configuracao.DiretorioCgroups, nome);
        Directory.CreateDirectory(grupo);
        await File.WriteAllTextAsync(Path.Combine(grupo, "memory.max"), "268435456");
        await File.WriteAllTextAsync(Path.Combine(grupo, "memory.swap.max"), "0");
        var inicio = new ProcessStartInfo("/usr/bin/python3") { UseShellExecute = false };
        inicio.ArgumentList.Add("-c"); inicio.ArgumentList.Add("import time; time.sleep(60)");
        using var orfao = Process.Start(inicio)!;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(grupo, "cgroup.procs"), orfao.Id.ToString());
            var removidos = await new ProcessadorImagemIsolado(_configuracao).RetomarTemporariosAsync(CancellationToken.None);
            Assert.AreEqual(1, removidos);
            await orfao.WaitForExitAsync();
            Assert.IsFalse(Directory.Exists(privado));
            Assert.IsFalse(Directory.Exists(grupo));
            Assert.IsTrue(Directory.Exists(alheio));
        }
        finally
        {
            if (!orfao.HasExited) orfao.Kill(true);
            await orfao.WaitForExitAsync();
            if (Directory.Exists(grupo)) Directory.Delete(grupo);
        }
    }

    private ConfiguracaoProcessadorImagem Script(string codigo)
    {
        var caminho = Path.Combine(_temporarios, Guid.NewGuid().ToString("N") + ".py");
        File.WriteAllText(caminho, codigo);
        var python = File.ResolveLinkTarget("/usr/bin/python3", true)!.FullName;
        return _configuracao with { Executavel = python, Processador = caminho };
    }

    private static byte[] InserirChunk(byte[] png, string tipo, byte[] dados)
    {
        var chunk = Chunk(tipo, dados);
        return [.. png[..33], .. chunk, .. png[33..]];
    }

    private static byte[] Chunk(string tipo, byte[] dados)
    {
        var chunk = new byte[dados.Length + 12];
        BinaryPrimitives.WriteInt32BigEndian(chunk, dados.Length);
        System.Text.Encoding.ASCII.GetBytes(tipo).CopyTo(chunk, 4);
        dados.CopyTo(chunk, 8);
        GravarCrc(chunk.AsSpan(4, dados.Length + 4), chunk.AsSpan(dados.Length + 8));
        return chunk;
    }

    private static void GravarCrc(ReadOnlySpan<byte> dados, Span<byte> destino)
    {
        uint crc = uint.MaxValue;
        foreach (var b in dados)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0);
        }
        BinaryPrimitives.WriteUInt32BigEndian(destino, ~crc);
    }

    private static byte[] PngIndexado(int largura, int altura)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, largura);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), altura);
        ihdr[8] = 8; ihdr[9] = 3;
        var paleta = new byte[256 * 3];
        new Random(61).NextBytes(paleta);
        using var compactado = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compactado, System.IO.Compression.CompressionLevel.SmallestSize, true))
        {
            var linha = new byte[largura + 1];
            var random = new Random(19);
            // Quatro índices aleatórios comprimem a ~4 MiB, mas cores RGB expandem a >5 MiB.
            for (var y = 0; y < altura; y++)
            {
                for (var x = 1; x <= largura; x++) linha[x] = (byte)random.Next(4);
                zlib.Write(linha);
            }
        }
        return [137, 80, 78, 71, 13, 10, 26, 10, .. Chunk("IHDR", ihdr), .. Chunk("PLTE", paleta),
            .. Chunk("IDAT", compactado.ToArray()), .. Chunk("IEND", [])];
    }
}
