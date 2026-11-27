using System.Net;
using System.Runtime.Versioning;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Locadora.Ensaios.FotosR2.Retencao;
using Locadora.Infrastructure.Modulos.Fotos;
using Microsoft.Extensions.Configuration;

namespace Locadora.Ensaios.FotosR2.LeituraAusente;

[SupportedOSPlatform("linux")]
internal static class EntradaLeituraAusente
{
    // Sinal não secreto não substitui a autorização específica via question.
    public static async Task<int> ExecutarAsync(string backend)
    {
        if (Environment.GetEnvironmentVariable("LOCADORA_R2_LEITURA_AUSENTE_AUTORIZADA") != "UM_GET_AUSENTE")
        {
            Console.Error.WriteLine("BLOCKED: GET único exige nova autorização específica via question. Sem leitura de segredos/manifesto ou rede.");
            return 2;
        }
        try
        {
            var permissao = PermissaoLeituraAusente.Abrir(backend);
            // Somente numa futura execução autorizada, não na qualificação offline.
            var segredos = new ConfigurationBuilder().AddUserSecrets("locadora-de-veiculos-dev").Build();
            if (segredos["Fotos:Provedor"] != "R2" || segredos["Fotos:R2:Bucket"] != ManifestoRetencao.Bucket
                || !segredos.GetValue<bool>("Fotos:R2:BucketPrivadoConfirmado")
                || !Uri.TryCreate(segredos["Fotos:R2:Endpoint"], UriKind.Absolute, out var configurado)
                || configurado.AbsoluteUri != permissao.Endpoint.AbsoluteUri
                || configurado.OriginalString.TrimEnd('/') != configurado.GetLeftPart(UriPartial.Authority))
                throw new InvalidOperationException();
            var accessKey = segredos["Ensaios:FotosR2:SomenteLeitura:AccessKey"] ?? "";
            var secretKey = segredos["Ensaios:FotosR2:SomenteLeitura:SecretKey"] ?? "";
            var configuracao = new ConfiguracaoR2
            {
                Endpoint = permissao.Endpoint.AbsoluteUri, Bucket = ManifestoRetencao.Bucket,
                BucketPrivadoConfirmado = true,
                AccessKey = accessKey, SecretKey = secretKey,
            };
            var opcoes = configuracao.CriarOpcoes();
            opcoes.MaxErrorRetry = 0;
            opcoes.HttpClientFactory = new FabricaHttpRetencao(permissao.ConferirTransporte);
            using var s3 = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), opcoes);
            using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var esperado = await ObservarAsync(s3, permissao, prazo.Token);
            Console.WriteLine(esperado
                ? "PASS GET único real: 404/NoSuchKey; ausência de objeto, não outage. Manifesto/catálogo preservados. Não encerra automaticamente 8.3."
                : "FAIL GET único: resposta inesperada; não comprova 404/NoSuchKey, sem repetição ou detalhe bruto.");
            return esperado ? 0 : 1;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("FAIL GET único: prova não obtida. Sem repetição; detalhes suprimidos; manifesto/catálogo preservados.");
            return 1;
        }
    }

    // Mantém StatusCode/ErrorCode do SDK; o adaptador de produto os traduz para 503.
    internal static async Task<bool> ObservarAsync(IAmazonS3 s3, PermissaoLeituraAusente permissao, CancellationToken token)
    {
        permissao.IniciarLeitura();
        try
        {
            using var resposta = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = ManifestoRetencao.Bucket, Key = permissao.Chave,
            }, token);
            return false; // Não lê/persiste bytes numa resposta inesperada de sucesso.
        }
        catch (AmazonS3Exception erro) when (erro.StatusCode == HttpStatusCode.NotFound && erro.ErrorCode == "NoSuchKey")
        {
            permissao.ConferirDisco();
            return permissao.Encaminhamentos == 1;
        }
    }
}
