using System.Buffers.Binary;
using Locadora.ProtocoloFotos;
using SkiaSharp;

namespace Locadora.ProcessadorImagens;

internal sealed class ImagemRejeitadaException(byte codigo) : Exception
{
    public byte Codigo { get; } = codigo;
}
internal sealed record ResultadoImagem(byte[] Bytes, string Mime, int Largura, int Altura);

internal static class SanitizadorImagem
{
    public static ResultadoImagem Sanear(byte[] bytes, string mime)
    {
        var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        if ((!png && !jpeg) || mime != (png ? "image/png" : "image/jpeg"))
            Rejeitar(ProtocoloProcessador.Formato);
        if (png) ValidarPng(bytes); else ValidarJpeg(bytes);
        if (jpeg) ValidadorJpegEstrito.Validar(bytes);
        using var dados = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(dados);
        if (codec is null || codec.FrameCount > 1
            || codec.EncodedFormat != (png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg))
            Rejeitar();
        var largura = codec!.Info.Width;
        var altura = codec.Info.Height;
        ValidarDimensoes(largura, altura);
        using var srgb = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(largura, altura, SKColorType.Rgba8888, SKAlphaType.Premul, srgb);
        using var origem = new SKBitmap(info);
        if (codec.GetPixels(info, origem.GetPixels()) != SKCodecResult.Success)
            Rejeitar();
        var orientacao = codec.EncodedOrigin;
        var trocaEixos = orientacao is >= SKEncodedOrigin.LeftTop and <= SKEncodedOrigin.LeftBottom;
        var saidaInfo = new SKImageInfo(trocaEixos ? altura : largura, trocaEixos ? largura : altura,
            SKColorType.Rgba8888, SKAlphaType.Premul, srgb);
        using var destino = new SKBitmap(saidaInfo);
        using (var canvas = new SKCanvas(destino))
        {
            canvas.Clear(SKColors.Transparent);
            // Coordenadas contínuas de borda (não índices de pixel).
            var matriz = orientacao switch
            {
                SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, largura, 0, 1, 0, 0, 0, 1),
                SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, largura, 0, -1, altura, 0, 0, 1),
                SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, altura, 0, 0, 1),
                SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightTop => new SKMatrix(0, -1, altura, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, altura, -1, 0, largura, 0, 0, 1),
                SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, largura, 0, 0, 1),
                _ => SKMatrix.Identity
            };
            canvas.SetMatrix(matriz);
            canvas.DrawBitmap(origem, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            canvas.Flush();
        }
        using var imagem = SKImage.FromBitmap(destino);
        using var codificada = imagem.Encode(png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 90);
        if (codificada is null) Rejeitar();
        if (codificada!.Size > ProtocoloProcessador.MaximoBytes) Rejeitar(ProtocoloProcessador.Excesso);
        return new(codificada.ToArray(), mime, saidaInfo.Width, saidaInfo.Height);
    }

    private static void ValidarDimensoes(int largura, int altura)
    {
        if (largura <= 0 || altura <= 0 || (long)largura * altura > ProtocoloProcessador.MaximoPixels)
            Rejeitar();
    }

    private static void ValidarPng(ReadOnlySpan<byte> bytes)
    {
        var posicao = 8;
        var cabecalho = false;
        var dados = false;
        var encerrouDados = false;
        while (posicao < bytes.Length)
        {
            if (bytes.Length - posicao < 12) Rejeitar();
            var tamanho = BinaryPrimitives.ReadUInt32BigEndian(bytes[posicao..]);
            if (tamanho > bytes.Length - posicao - 12) Rejeitar();
            var tipo = bytes.Slice(posicao + 4, 4);
            var conteudo = bytes.Slice(posicao + 8, (int)tamanho);
            var esperado = BinaryPrimitives.ReadUInt32BigEndian(bytes[(posicao + 8 + (int)tamanho)..]);
            if (Crc(bytes.Slice(posicao + 4, 4 + (int)tamanho)) != esperado) Rejeitar();
            if (!cabecalho && !tipo.SequenceEqual("IHDR"u8)) Rejeitar();
            if (tipo.SequenceEqual("acTL"u8) || tipo.SequenceEqual("fcTL"u8) || tipo.SequenceEqual("fdAT"u8))
                Rejeitar();
            if (tipo.SequenceEqual("IHDR"u8))
            {
                if (cabecalho || tamanho != 13) Rejeitar();
                ValidarDimensoes(BinaryPrimitives.ReadInt32BigEndian(conteudo),
                    BinaryPrimitives.ReadInt32BigEndian(conteudo[4..]));
                cabecalho = true;
            }
            else if (tipo.SequenceEqual("IDAT"u8))
            {
                if (encerrouDados) Rejeitar();
                dados = true;
            }
            else
            {
                if (dados) encerrouDados = true;
                if (tipo.SequenceEqual("IEND"u8))
                {
                    if (!dados || tamanho != 0 || posicao + 12 != bytes.Length) Rejeitar();
                    return;
                }
                if ((tipo[0] & 32) == 0 && !tipo.SequenceEqual("PLTE"u8)) Rejeitar();
            }
            posicao += 12 + (int)tamanho;
        }
        Rejeitar();
    }

    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0);
        }
        return ~crc;
    }

    private static void ValidarJpeg(ReadOnlySpan<byte> bytes)
    {
        var posicao = 2;
        var varredura = false;
        var quadro = false;
        var teveVarredura = false;
        while (posicao < bytes.Length)
        {
            if (varredura)
            {
                while (posicao < bytes.Length && bytes[posicao] != 255) posicao++;
            }
            if (posicao >= bytes.Length || bytes[posicao++] != 255) Rejeitar();
            while (posicao < bytes.Length && bytes[posicao] == 255) posicao++;
            if (posicao >= bytes.Length) Rejeitar();
            var marcador = bytes[posicao++];
            if (varredura && (marcador == 0 || marcador is >= 0xd0 and <= 0xd7)) continue;
            varredura = false;
            if (marcador == 0xd9)
            {
                if (!quadro || !teveVarredura || posicao != bytes.Length) Rejeitar();
                return;
            }
            if (marcador is 0 or 0xd8 || marcador is >= 0xd0 and <= 0xd7) Rejeitar();
            if (marcador == 1) continue;
            if (bytes.Length - posicao < 2) Rejeitar();
            var tamanho = BinaryPrimitives.ReadUInt16BigEndian(bytes[posicao..]);
            if (tamanho < 2 || tamanho > bytes.Length - posicao) Rejeitar();
            if (marcador is 0xc0 or 0xc1 or 0xc2)
            {
                if (quadro || tamanho < 8) Rejeitar();
                ValidarDimensoes(BinaryPrimitives.ReadUInt16BigEndian(bytes[(posicao + 5)..]),
                    BinaryPrimitives.ReadUInt16BigEndian(bytes[(posicao + 3)..]));
                quadro = true;
            }
            if (marcador == 0xda)
            {
                if (!quadro || tamanho < 6) Rejeitar();
                varredura = true;
                teveVarredura = true;
            }
            posicao += tamanho;
        }
        Rejeitar();
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Rejeitar(byte codigo = ProtocoloProcessador.Invalida) => throw new ImagemRejeitadaException(codigo);
}
