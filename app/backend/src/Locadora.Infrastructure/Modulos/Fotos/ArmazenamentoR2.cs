using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Locadora.Application.Modulos.Fotos;

namespace Locadora.Infrastructure.Modulos.Fotos;

// Não usar record: ToString não deve revelar credenciais.
public sealed class ConfiguracaoR2
{
    public required string Endpoint { private get; init; }
    public required string Bucket { get; init; }
    public required string AccessKey { private get; init; }
    public required string SecretKey { private get; init; }
    public required bool BucketPrivadoConfirmado { private get; init; }

    public void Validar()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !uri.IsDefaultPort || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.UserInfo.Length != 0 || !uri.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.Ordinal)
            || uri.Host.Split('.')[0].Length != 32 || !uri.Host.Split('.')[0].All(char.IsAsciiHexDigit)
            || !BucketPrivadoConfirmado || Bucket.Length is < 3 or > 63
            || Bucket.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')
            || Bucket != Bucket.ToLowerInvariant() || !char.IsAsciiLetterOrDigit(Bucket[0])
            || !char.IsAsciiLetterOrDigit(Bucket[^1])
            || string.IsNullOrWhiteSpace(AccessKey) || string.IsNullOrWhiteSpace(SecretKey))
            throw new ArmazenamentoObjetoException();
    }

    public AmazonS3Config CriarOpcoes()
    {
        Validar();
        return new AmazonS3Config
        {
            ServiceURL = Endpoint, AuthenticationRegion = "auto", ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            Timeout = TimeSpan.FromSeconds(30), MaxErrorRetry = 0,
        };
    }

    public IAmazonS3 CriarCliente() => new AmazonS3Client(new BasicAWSCredentials(AccessKey, SecretKey), CriarOpcoes());
}

public sealed class ArmazenamentoR2 : IArmazenamentoObjeto
{
    private readonly IAmazonS3 cliente;
    private readonly string bucket;

    // O ciclo de vida do cliente é responsabilidade do composition root.
    public ArmazenamentoR2(IAmazonS3 cliente, ConfiguracaoR2 configuracao)
    {
        configuracao.Validar();
        this.cliente = cliente;
        bucket = configuracao.Bucket;
    }

    public async Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken cancelamento)
    {
        objeto.Conferir(bytes.Span);
        using var prazo = Prazo(cancelamento);
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        try
        {
            var resposta = await cliente.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket, Key = objeto.Chave, ContentType = objeto.Mime,
                InputStream = stream, AutoCloseStream = false, AutoResetStreamPosition = false,
                DisablePayloadSigning = true, DisableDefaultChecksumValidation = true, UseChunkEncoding = false,
            }, prazo.Token);
            if (resposta.HttpStatusCode != HttpStatusCode.OK) throw new ArmazenamentoObjetoException();
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        { throw new ArmazenamentoObjetoException(); }
        catch (Exception e) when (e is AmazonClientException or AmazonServiceException or IOException)
        { throw new ArmazenamentoObjetoException(); }
    }

    public async Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken cancelamento)
    {
        objeto.Validar();
        using var prazo = Prazo(cancelamento);
        try
        {
            using var resposta = await cliente.GetObjectAsync(new GetObjectRequest
            { BucketName = bucket, Key = objeto.Chave }, prazo.Token);
            if (resposta.HttpStatusCode != HttpStatusCode.OK || resposta.ContentLength != objeto.TamanhoBytes
                || resposta.Headers.ContentType != objeto.Mime)
                throw new ArmazenamentoObjetoException();
            var bytes = new byte[objeto.TamanhoBytes];
            await resposta.ResponseStream.ReadExactlyAsync(bytes, prazo.Token);
            if (await resposta.ResponseStream.ReadAsync(new byte[1], prazo.Token) != 0)
                throw new ArmazenamentoObjetoException();
            objeto.Conferir(bytes);
            return bytes;
        }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        { throw new ArmazenamentoObjetoException(); }
        catch (Exception e) when (e is AmazonClientException or AmazonServiceException or IOException)
        { throw new ArmazenamentoObjetoException(); }
    }

    public async Task RemoverAsync(ObjetoFoto objeto, CancellationToken cancelamento)
    {
        objeto.Validar();
        using var prazo = Prazo(cancelamento);
        try
        {
            var resposta = await cliente.DeleteObjectAsync(new DeleteObjectRequest
            { BucketName = bucket, Key = objeto.Chave }, prazo.Token);
            if (resposta.HttpStatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.OK))
                throw new ArmazenamentoObjetoException();
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound && e.ErrorCode == "NoSuchKey") { }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        { throw new ArmazenamentoObjetoException(); }
        catch (Exception e) when (e is AmazonClientException or AmazonServiceException or IOException)
        { throw new ArmazenamentoObjetoException(); }
    }

    private static CancellationTokenSource Prazo(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var prazo = CancellationTokenSource.CreateLinkedTokenSource(token);
        prazo.CancelAfter(TimeSpan.FromSeconds(30));
        return prazo;
    }

    public async Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken cancelamento)
    {
        objeto.Validar();
        using var prazo = Prazo(cancelamento);
        try
        {
            var resposta = await cliente.GetObjectMetadataAsync(new GetObjectMetadataRequest
            { BucketName = bucket, Key = objeto.Chave }, prazo.Token);
            if (resposta.HttpStatusCode != HttpStatusCode.OK) throw new ArmazenamentoObjetoException();
            return false;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound
            && e.ErrorCode is "NoSuchKey" or "NotFound") { return true; }
        catch (OperationCanceledException) when (!cancelamento.IsCancellationRequested)
        { throw new ArmazenamentoObjetoException(); }
        catch (Exception e) when (e is AmazonClientException or AmazonServiceException or IOException)
        { throw new ArmazenamentoObjetoException(); }
    }
}
