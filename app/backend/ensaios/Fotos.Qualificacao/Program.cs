using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using SkiaSharp;

// Ensaio offline: nunca constrói cliente autenticado nem realiza requisições.
using var cor = SKColorSpace.CreateSrgb();
using var bitmap = new SKBitmap(new SKImageInfo(32, 24, SKColorType.Rgba8888, SKAlphaType.Premul, cor));
bitmap.Erase(SKColors.CornflowerBlue);
using var imagem = SKImage.FromBitmap(bitmap);
using var png = imagem.Encode(SKEncodedImageFormat.Png, 100);
using var jpeg = imagem.Encode(SKEncodedImageFormat.Jpeg, 90);
if (args.Length == 2 && args[0] == "--corpus")
{
    Directory.CreateDirectory(args[1]);
    // Mesma construção da fixture que falhou em ProcessamentoImagemTests.
    using var bitmapCorpus = new SKBitmap(32, 24);
    bitmapCorpus.Erase(SKColors.Transparent);
    using (var canvas = new SKCanvas(bitmapCorpus))
    using (var tinta = new SKPaint { Color = SKColors.Red })
        canvas.DrawRect(0, 0, 16, 24, tinta);
    using var imagemCorpus = SKImage.FromBitmap(bitmapCorpus);
    using var jpegCorpus = imagemCorpus.Encode(SKEncodedImageFormat.Jpeg, 90);
    var bytes = jpegCorpus.ToArray();
    var sos = Enumerable.Range(2, bytes.Length - 3).First(i => bytes[i] == 255 && bytes[i + 1] == 218);
    var inicioScan = sos + 2 + System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(sos + 2));
    File.WriteAllBytes(Path.Combine(args[1], "valido.jpg"), bytes);
    File.WriteAllBytes(Path.Combine(args[1], "sem-scan.jpg"), [.. bytes[..inicioScan], 255, 217]);
    File.WriteAllBytes(Path.Combine(args[1], "scan-parcial.jpg"), [.. bytes[..(inicioScan + 1)], 255, 217]);
    File.WriteAllBytes(Path.Combine(args[1], "scan-metade.jpg"), [.. bytes[..(inicioScan + (bytes.Length - inicioScan - 2) / 2)], 255, 217]);
    File.WriteAllBytes(Path.Combine(args[1], "sem-eoi.jpg"), bytes[..^2]);
    File.WriteAllBytes(Path.Combine(args[1], "cabecalho-incompleto.jpg"), bytes[..20]);
}
foreach (var bytes in new[] { png, jpeg })
{
    using var codec = SKCodec.Create(bytes);
    using var destino = new SKBitmap(codec.Info);
    if (codec.GetPixels(destino.Info, destino.GetPixels()) != SKCodecResult.Success)
        throw new InvalidOperationException("Falha de decode integral.");
}
var configuracao = new AmazonS3Config
{
    ServiceURL = "https://r2-qualificacao.invalid",
    AuthenticationRegion = "auto",
    ForcePathStyle = true,
    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
};
var requisicao = new PutObjectRequest
{
    DisablePayloadSigning = true,
    DisableDefaultChecksumValidation = true,
    UseChunkEncoding = false
};
if (configuracao.AuthenticationRegion != "auto" || requisicao.UseChunkEncoding)
    throw new InvalidOperationException("Configuração incompatível.");
Console.WriteLine("PASS: JPEG/PNG nativos e superfície de configuração S3 v4, offline.");
