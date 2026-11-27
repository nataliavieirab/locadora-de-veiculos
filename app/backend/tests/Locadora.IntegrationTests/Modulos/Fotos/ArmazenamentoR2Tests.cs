using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Locadora.Application.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.IntegrationTests.Suporte;
using static Locadora.IntegrationTests.Modulos.Fotos.FotosHttpTests;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
public sealed class ArmazenamentoR2Tests
{
    private static ConfiguracaoR2 Config(string? endpoint = null, bool privado = true) => new()
    {
        Endpoint = endpoint ?? "https://00000000000000000000000000000000.r2.cloudflarestorage.com",
        Bucket = "ensaio-privado", AccessKey = "sintetica", SecretKey = "sintetica",
        BucketPrivadoConfirmado = privado,
    };

    [TestMethod]
    public void ConfiguracaoFalhaFechadaEMajorQuatroSemChecksumOpcionalOuRetry()
    {
        foreach (var endpoint in new[] { "http://externo", "https://publico.r2.dev", "https://externo.invalid",
            "https://00000000000000000000000000000000.r2.cloudflarestorage.com/x" })
            Assert.ThrowsExactly<ArmazenamentoObjetoException>(() => Config(endpoint).CriarOpcoes());
        Assert.ThrowsExactly<ArmazenamentoObjetoException>(() => Config(privado: false).CriarOpcoes());
        var opcoes = Config().CriarOpcoes();
        Assert.AreEqual("auto", opcoes.AuthenticationRegion);
        Assert.IsTrue(opcoes.ForcePathStyle);
        Assert.AreEqual(RequestChecksumCalculation.WHEN_REQUIRED, opcoes.RequestChecksumCalculation);
        Assert.AreEqual(ResponseChecksumValidation.WHEN_REQUIRED, opcoes.ResponseChecksumValidation);
        Assert.AreEqual(0, opcoes.MaxErrorRetry);
        Assert.IsFalse(Config().ToString()!.Contains("sintetica", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ContratoControladoConfereBytesMimeDescarteEDeleteIdempotente()
    {
        using var cliente = new S3Controlado();
        var storage = new ArmazenamentoR2(cliente, Config());
        var bytes = new byte[] { 1, 2, 3 };
        var objeto = new ObjetoFoto($"{Guid.NewGuid():N}/{Guid.NewGuid():N}", "image/png", 3, SHA256.HashData(bytes));
        await storage.GravarAsync(objeto, bytes, default);
        Assert.IsFalse(cliente.Entrada!.CanRead);
        CollectionAssert.AreEqual(bytes, await storage.LerAsync(objeto, default));
        Assert.IsFalse(cliente.Saida!.CanRead);
        cliente.Bytes = [3, 2, 1];
        await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.LerAsync(objeto, default));
        Assert.IsFalse(cliente.Saida!.CanRead);
        cliente.Bytes = [1, 2, 3, 4];
        await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.LerAsync(objeto, default));
        Assert.IsFalse(cliente.Saida!.CanRead);
        cliente.Bytes = [1];
        await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.LerAsync(objeto, default));
        cliente.Mime = "text/html";
        await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.LerAsync(objeto, default));
        Assert.IsFalse(cliente.Saida!.CanRead);
        await storage.RemoverAsync(objeto, default);
        await storage.RemoverAsync(objeto, default);
        Assert.IsTrue(await storage.ConfirmarAusenciaAsync(objeto, default));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => storage.GravarAsync(objeto, bytes, new(true)));
        cliente.Falhar = true;
        await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.GravarAsync(objeto, bytes, default));
        Assert.IsFalse(cliente.Entrada!.CanRead);
        await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.LerAsync(objeto, default));
        await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.RemoverAsync(objeto, default));
    }

    [TestMethod]
    public async Task HttpTraduzFalhasS3ControladasEm503ERecuperaSemAlterarReferenciaOuVersao()
    {
        // Só overrides do SDK: zero transporte/rede/credencial real. Não prova R2.
        using var s3 = new S3Controlado();
        using var fabrica = Fabrica(new ArmazenamentoR2(s3, Config()),
            destino: new DestinoFotos(ProvedorFoto.R2, "ensaio-privado"));
        using var cliente = await ClienteAsync(fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        using var envio = await EnviarAsync(cliente, id, etag);
        Assert.AreEqual(HttpStatusCode.OK, envio.StatusCode);
        using var original = await cliente.GetAsync($"/api/v1/veiculos/{id}");
        var referencia = (await ApiCliente.JsonAsync(original)).GetProperty("foto").GetRawText();
        var versao = original.Headers.ETag!.ToString();
        var erros = new (HttpStatusCode Status, string Codigo)[]
        {
            (HttpStatusCode.Forbidden, "AccessDenied"),
            (HttpStatusCode.NotFound, "NoSuchKey"),
            (HttpStatusCode.ServiceUnavailable, "ServiceUnavailable"),
        };
        // Não confundir 404 do objeto de uma foto ativa com "veículo sem foto".
        foreach (var (status, codigo) in erros)
        {
            s3.ErroLeitura = new AmazonS3Exception("detalhe-s3-controlado-nao-expor")
                { StatusCode = status, ErrorCode = codigo };
            await FalharERecuperarAsync();
        }
        // Cancelamento interno simulado, não timeout observado no provedor.
        foreach (var erro in new OperationCanceledException[]
            { new("detalhe-s3-controlado-nao-expor"), new TaskCanceledException("detalhe-s3-controlado-nao-expor") })
        {
            s3.ErroLeitura = erro;
            await FalharERecuperarAsync();
        }
        s3.Bytes = [3, 2, 1]; // hash divergente, mesmo tamanho
        await FalharERecuperarAsync();
        s3.Bytes = [1]; // stream truncado
        await FalharERecuperarAsync();
        s3.Mime = "text/html";
        await FalharERecuperarAsync();

        async Task FalharERecuperarAsync()
        {
            var antes = s3.Leituras;
            using var falha = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, falha.StatusCode);
            Assert.AreEqual(antes + 1, s3.Leituras);
            Assert.IsTrue(falha.Headers.CacheControl!.NoStore);
            Assert.IsNull(falha.Headers.Location);
            var problema = await ApiCliente.JsonAsync(falha);
            Assert.AreEqual("foto_indisponivel", problema.GetProperty("code").GetString());
            Assert.IsFalse(problema.GetRawText().Contains("detalhe-s3-controlado", StringComparison.Ordinal));
            using var detalhe = await cliente.GetAsync($"/api/v1/veiculos/{id}");
            Assert.AreEqual(referencia, (await ApiCliente.JsonAsync(detalhe)).GetProperty("foto").GetRawText());
            Assert.AreEqual(versao, detalhe.Headers.ETag!.ToString());
            s3.ErroLeitura = null;
            s3.Bytes = [1, 2, 3];
            s3.Mime = "image/png";
            using var recuperada = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
            Assert.AreEqual(HttpStatusCode.OK, recuperada.StatusCode);
            Assert.AreEqual(antes + 2, s3.Leituras);
            Assert.IsTrue(recuperada.Headers.CacheControl!.Private && recuperada.Headers.CacheControl.NoStore);
            Assert.AreEqual("image/png", recuperada.Content.Headers.ContentType!.MediaType);
            Assert.AreEqual("nosniff", recuperada.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.AreEqual(3L, recuperada.Content.Headers.ContentLength);
            Assert.IsNull(recuperada.Headers.Location);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await recuperada.Content.ReadAsByteArrayAsync());
            Assert.IsFalse(s3.Saida!.CanRead);
            using var posterior = await cliente.GetAsync($"/api/v1/veiculos/{id}");
            Assert.AreEqual(referencia, (await ApiCliente.JsonAsync(posterior)).GetProperty("foto").GetRawText());
            Assert.AreEqual(versao, posterior.Headers.ETag!.ToString());
        }
    }

    [TestMethod]
    public async Task CancelamentoInternoViraIndisponibilidadeSemConfirmarAusencia()
    {
        using var cliente = new S3Controlado();
        var storage = new ArmazenamentoR2(cliente, Config());
        byte[] bytes = [1, 2, 3];
        var objeto = new ObjetoFoto($"{Guid.NewGuid():N}/{Guid.NewGuid():N}", "image/png", 3, SHA256.HashData(bytes));
        foreach (var erro in new OperationCanceledException[] { new(), new TaskCanceledException() })
        {
            cliente.ErroOperacao = erro;
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.GravarAsync(objeto, bytes, default));
            Assert.IsFalse(cliente.Entrada!.CanRead);
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.LerAsync(objeto, default));
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.RemoverAsync(objeto, default));
            await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.ConfirmarAusenciaAsync(objeto, default));
        }
        cliente.ErroOperacao = null;
        cliente.CriarSaida = () => new StreamCancelavel(bytes, () => throw new OperationCanceledException());
        await Assert.ThrowsExactlyAsync<ArmazenamentoObjetoException>(() => storage.LerAsync(objeto, default));
        Assert.IsFalse(cliente.Saida!.CanRead);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => storage.RemoverAsync(objeto, new(true)));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => storage.ConfirmarAusenciaAsync(objeto, new(true)));
    }

    [TestMethod]
    public async Task CancelamentoDurantePutELeituraFechaStreams()
    {
        using var cliente = new S3Controlado();
        var storage = new ArmazenamentoR2(cliente, Config());
        byte[] bytes = [1, 2, 3];
        var objeto = new ObjetoFoto($"{Guid.NewGuid():N}/{Guid.NewGuid():N}", "image/png", 3, SHA256.HashData(bytes));
        using var put = new CancellationTokenSource();
        cliente.AntesRespostaPut = put.Cancel;
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => storage.GravarAsync(objeto, bytes, put.Token));
        Assert.IsFalse(cliente.Entrada!.CanRead);
        using var get = new CancellationTokenSource();
        cliente.CriarSaida = () => new StreamCancelavel(bytes, get.Cancel);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => storage.LerAsync(objeto, get.Token));
        Assert.IsFalse(cliente.Saida!.CanRead);
    }

    private sealed class S3Controlado() : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config
    { ServiceURL = "https://offline.invalid", AuthenticationRegion = "auto" })
    {
        public byte[] Bytes { get; set; } = [];
        public string Mime { get; set; } = "image/png";
        public Stream? Entrada { get; private set; }
        public Stream? Saida { get; private set; }
        public bool Falhar { get; set; }
        public Exception? ErroLeitura { get; set; }
        public Exception? ErroOperacao { get; set; }
        public int Leituras { get; private set; }
        public Action? AntesRespostaPut { get; set; }
        public Func<Stream>? CriarSaida { get; set; }

        public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
        {
            Entrada = request.InputStream;
            Assert.IsTrue(request.DisablePayloadSigning);
            Assert.IsTrue(request.DisableDefaultChecksumValidation);
            Assert.IsFalse(request.UseChunkEncoding);
            Assert.IsFalse(request.AutoCloseStream);
            Assert.IsTrue(cancellationToken.CanBeCanceled);
            Assert.AreEqual("ensaio-privado", request.BucketName);
            using var copia = new MemoryStream();
            await Entrada.CopyToAsync(copia, cancellationToken);
            Bytes = copia.ToArray();
            AntesRespostaPut?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (ErroOperacao is { } erro) throw erro;
            if (Falhar) throw new AmazonS3Exception("falha sintética");
            return new() { HttpStatusCode = HttpStatusCode.OK, ETag = "nao-e-sha256" };
        }

        public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken = default)
        {
            Leituras++;
            if (ErroOperacao is { } falha) throw falha;
            if (ErroLeitura is { } erro) throw erro;
            if (Falhar) throw new AmazonS3Exception("falha sintética");
            Saida = CriarSaida?.Invoke() ?? new MemoryStream(Bytes);
            var resposta = new GetObjectResponse { HttpStatusCode = HttpStatusCode.OK, ContentLength = 3, ResponseStream = Saida };
            resposta.Headers.ContentType = Mime;
            return Task.FromResult(resposta);
        }

        public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default)
        {
            if (ErroOperacao is { } erro) throw erro;
            if (Falhar) throw new AmazonS3Exception("falha sintética");
            return Task.FromResult(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });
        }
        public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken cancellationToken = default)
            => throw ErroOperacao ?? new AmazonS3Exception("ausente") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NotFound" };
    }

    private sealed class StreamCancelavel(byte[] bytes, Action cancelar) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancelar();
            cancellationToken.ThrowIfCancellationRequested();
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
