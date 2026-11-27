using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Locadora.Api.Modulos.Fotos;

public static class LeitorMultipartFoto
{
    public const int MaximoCorpo = FotoObjeto.MaximoBytes + 65536;

    public static async Task<(MemoryStream Arquivo, string Mime)> LerAsync(HttpRequest request, CancellationToken token)
    {
        if (request.ContentLength > MaximoCorpo) throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Excesso);
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var tipo)
            || !tipo.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Formato);
        var boundary = HeaderUtilities.RemoveQuotes(tipo.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > 128) throw Invalida();
        // Conta o envelope inteiro, inclusive epílogo e corpo sem Content-Length.
        using var corpo = new StreamContado(request.Body, MaximoCorpo);
        var leitor = new MultipartReader(boundary, corpo)
        {
            HeadersCountLimit = 16, HeadersLengthLimit = 65536,
        };
        var arquivo = new MemoryStream();
        try
        {
            var parte = await leitor.ReadNextSectionAsync(token) ?? throw Invalida();
            if (!ContentDispositionHeaderValue.TryParse(parte.ContentDisposition, out var disposicao)
                || !disposicao.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase)
                || HeaderUtilities.RemoveQuotes(disposicao.Name).Value != "arquivo"
                || (!disposicao.FileName.HasValue && !disposicao.FileNameStar.HasValue)) throw Invalida();
            var mime = parte.ContentType;
            if (mime is not ("image/png" or "image/jpeg"))
                throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Formato);
            var bloco = new byte[81920];
            int lidos;
            while ((lidos = await parte.Body.ReadAsync(bloco, token)) > 0)
            {
                if (arquivo.Length + lidos > FotoObjeto.MaximoBytes)
                    throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Excesso);
                await arquivo.WriteAsync(bloco.AsMemory(0, lidos), token);
            }
            if (arquivo.Length == 0 || await leitor.ReadNextSectionAsync(token) is not null) throw Invalida();
            while (await corpo.ReadAsync(bloco, token) != 0) { }
            arquivo.Position = 0;
            return (arquivo, mime);
        }
        catch (BadHttpRequestException erro) when (erro.StatusCode == 413)
        {
            arquivo.Dispose();
            throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Excesso);
        }
        catch (InvalidDataException) { arquivo.Dispose(); throw Invalida(); }
        catch (IOException) { arquivo.Dispose(); throw Invalida(); }
        catch { arquivo.Dispose(); throw; }
    }

    private static ProcessamentoImagemException Invalida() => new(FalhaProcessamentoImagem.Invalida);

    private sealed class StreamContado(Stream origem, long maximo) : Stream
    {
        private long total;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            var lidos = await origem.ReadAsync(buffer, token);
            total += lidos;
            if (total > maximo) throw new ProcessamentoImagemException(FalhaProcessamentoImagem.Excesso);
            return lidos;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
            ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
