using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.Runtime;
using Amazon.S3;
using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Ensaios.FotosR2.Retencao;
using Locadora.Infrastructure.Modulos.Fotos;

namespace Locadora.Ensaios.FotosR2.LeituraAusente;

[SupportedOSPlatform("linux")]
internal static class QualificacaoLeituraAusente
{
    private const UnixFileMode ArquivoPrivado = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PastaPrivada = ArquivoPrivado | UnixFileMode.UserExecute;
    private static readonly Uri Endpoint = new("https://00000000000000000000000000000000.r2.cloudflarestorage.com/");

    public static async Task ExecutarAsync(string backend)
    {
        var raiz = Path.Combine(backend, "obj", "r2-leitura-qualificacao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz, PastaPrivada);
        var negadas = 0;
        async Task Negar(Func<Task> acao)
        {
            try { await acao(); }
            catch (Exception e) when (e is ArmazenamentoObjetoException or IOException
                or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException
                or KeyNotFoundException or HttpRequestException)
            { negadas++; return; }
            throw new Exception("Uma barreira não bloqueou o cenário sintético.");
        }
        Task Abrir(Cenario c) { PermissaoLeituraAusente.Abrir(c.Backend); return Task.CompletedTask; }
        var cenario = new Cenario(raiz);
        var permissao = PermissaoLeituraAusente.Abrir(cenario.Backend);
        var terminal = new Terminal(HttpStatusCode.NotFound, "NoSuchKey");
        using var http = new FabricaHttpRetencao(permissao.ConferirTransporte).CriarClienteSintetico(terminal);
        var destino = Endpoint + ManifestoRetencao.Bucket + "/" + permissao.Chave;
        await Negar(async () => { using var r = await http.GetAsync(destino); });
        Exigir(terminal.Chamadas == 0);
        permissao.IniciarLeitura();
        async Task Pedido(HttpMethod metodo, string uri, string? header = null)
        {
            using var p = new HttpRequestMessage(metodo, uri);
            if (header is not null) p.Headers.TryAddWithoutValidation(header, "sintetico");
            using var r = await http.SendAsync(p);
        }
        foreach (var metodo in new[] { HttpMethod.Put, HttpMethod.Head, HttpMethod.Delete,
            HttpMethod.Post, HttpMethod.Patch, HttpMethod.Options, HttpMethod.Trace })
            await Negar(() => Pedido(metodo, destino));
        foreach (var chave in cenario.Antigas.Concat(cenario.Chaves.Where(x => x != permissao.Chave))
            .Append(Guid.NewGuid().ToString("N") + "/" + Guid.NewGuid().ToString("N")))
            await Negar(() => Pedido(HttpMethod.Get, Endpoint + ManifestoRetencao.Bucket + "/" + chave));
        foreach (var uri in new[]
        {
            destino + "?list-type=2", destino + "?acl", destino + "?uploads",
            destino + "?uploadId=x&partNumber=1", destino + "?versionId=x",
            destino + "?X-Amz-Signature=x", destino + "?x-id=GetObject&acl",
            destino + "?x-id=HeadObject", destino + "#fragmento", destino + "/",
            destino.Replace("https:", "http:", StringComparison.Ordinal),
            destino.Replace("https://", "https://usuario@", StringComparison.Ordinal),
            destino.Replace(".com/", ".com:444/", StringComparison.Ordinal),
            destino.Replace(Endpoint.Host, "example.invalid", StringComparison.Ordinal),
            destino.Replace(ManifestoRetencao.Bucket, "outro-bucket", StringComparison.Ordinal),
            destino.Replace(permissao.Chave, permissao.Chave.Replace("/", "%2F"), StringComparison.Ordinal),
            destino.Replace(permissao.Chave, "prefixo/../" + permissao.Chave, StringComparison.Ordinal),
            Endpoint.AbsoluteUri, Endpoint + ManifestoRetencao.Bucket + "/",
        })
            await Negar(() => Pedido(HttpMethod.Get, uri));
        foreach (var header in new[] { "x-amz-copy-source", "x-amz-acl", "x-amz-grant-read",
            "x-amz-website-redirect-location", "Range", "If-None-Match", "Host" })
            await Negar(() => Pedido(HttpMethod.Get, destino, header));
        await Negar(async () =>
        {
            using var p = new HttpRequestMessage(HttpMethod.Get, destino) { Content = new StringContent("sintetico") };
            using var r = await http.SendAsync(p);
        });
        Exigir(terminal.Chamadas == 0);
        // Corrida e repetição direta do transporte: uma só chamada, mesmo sem o chamador.
        var resultados = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { using var r = await http.GetAsync(destino + "?x-id=GetObject"); return true; }
            catch (ArmazenamentoObjetoException) { return false; }
        }));
        Exigir(resultados.Count(x => x) == 1 && terminal.Chamadas == 1);
        negadas += resultados.Count(x => !x);
        await Negar(() => { permissao.IniciarLeitura(); return Task.CompletedTask; });
        cenario.ConferirIntacto();

        // SDK real, credenciais fictícias e terminal SEM sockets; mesma cadeia do modo futuro.
        var classificacoes = 0;
        foreach (var (status, codigo, esperado) in new[]
        {
            (HttpStatusCode.NotFound, "NoSuchKey", true),
            (HttpStatusCode.NotFound, "NoSuchBucket", false),
            (HttpStatusCode.Forbidden, "AccessDenied", false),
            (HttpStatusCode.ServiceUnavailable, "ServiceUnavailable", false),
            (HttpStatusCode.PreconditionFailed, "PreconditionFailed", false),
            (HttpStatusCode.OK, "", false),
            (HttpStatusCode.TemporaryRedirect, "TemporaryRedirect", false),
        })
        {
            var c = new Cenario(raiz);
            var p = PermissaoLeituraAusente.Abrir(c.Backend);
            var t = new Terminal(status, codigo);
            var opcoes = new ConfiguracaoR2
            {
                Endpoint = Endpoint.AbsoluteUri, Bucket = ManifestoRetencao.Bucket,
                AccessKey = "offline", SecretKey = "offline", BucketPrivadoConfirmado = true,
            }.CriarOpcoes();
            opcoes.HttpClientFactory = new FabricaSintetica(p, t);
            Exigir(opcoes.MaxErrorRetry == 0);
            using var sdk = new AmazonS3Client(new BasicAWSCredentials("offline", "offline"), opcoes);
            var resultado = false;
            try { resultado = await EntradaLeituraAusente.ObservarAsync(sdk, p, default); }
            catch (Exception) when (!esperado) { }
            Console.WriteLine($"OFFLINE classificação {(int)status}/{codigo}: resultado={resultado}; chamadas={t.Chamadas}; assinadas={t.Assinadas}.");
            Exigir(resultado == esperado && t.Chamadas == 1 && t.Assinadas == 1);
            await Negar(() => EntradaLeituraAusente.ObservarAsync(sdk, p, default));
            Exigir(t.Chamadas == 1);
            c.ConferirIntacto();
            classificacoes++;
        }

        foreach (var falha in new[] { "ausente", "json", "arquivo", "pasta", "symlink", "tamanho", "propriedade-duplicada",
            "deny-curta", "deny-duplicada", "intersecao", "bucket", "endpoint",
            "catalogo", "sem-ausencia", "ativa", "trabalho", "chave", "duplicada" })
        {
            var c = new Cenario(raiz);
            if (falha == "ausente") File.Move(c.Caminho, c.Caminho + ".preservado");
            else if (falha == "json") File.WriteAllText(c.Caminho, "{");
            else if (falha == "tamanho") File.WriteAllBytes(c.Caminho, new byte[1_048_577]);
            else if (falha == "propriedade-duplicada")
                File.WriteAllText(c.Caminho, File.ReadAllText(c.Caminho).Replace("\"bucket\":", "\"BUCKET\":\"duplicado\",\"bucket\":", StringComparison.Ordinal));
            else if (falha == "arquivo") File.SetUnixFileMode(c.Caminho, ArquivoPrivado | UnixFileMode.GroupRead);
            else if (falha == "pasta") File.SetUnixFileMode(Path.GetDirectoryName(c.Caminho)!, PastaPrivada | UnixFileMode.GroupExecute);
            else if (falha == "symlink")
            {
                File.Move(c.Caminho, c.Caminho + ".preservado");
                File.CreateSymbolicLink(c.Caminho, c.Caminho + ".preservado");
            }
            else
            {
                var j = JsonNode.Parse(File.ReadAllBytes(c.Caminho))!;
                if (falha == "deny-curta") j["denylist"]!.AsArray().RemoveAt(0);
                if (falha == "deny-duplicada") j["denylist"]![0] = c.Antigas[1];
                if (falha == "intersecao") j["denylist"]![0] = c.Chaves[0];
                if (falha == "bucket") j["bucket"] = "outro-bucket";
                if (falha == "endpoint") j["endpoint"] = "https://example.invalid/";
                if (falha == "catalogo") j["catalogo"] = j["catalogosProtegidos"]![0]!.GetValue<string>();
                if (falha == "sem-ausencia") j["objetos"]![0]!["AusenciaConfirmadaEmUtc"] = null;
                if (falha == "ativa") j["objetos"]![0]!["Estado"] = (int)EstadoFoto.Ativa;
                if (falha == "trabalho") j["objetos"]![0]!["EstadoTrabalho"] = (int)EstadoTrabalhoObjeto.Pendente;
                if (falha == "chave") j["objetos"]![0]!["Chave"] = "nao/canonica";
                if (falha == "duplicada") j["objetos"]![1] = j["objetos"]![0]!.DeepClone();
                File.WriteAllText(c.Caminho, j.ToJsonString());
            }
            await Negar(() => Abrir(c));
        }
        foreach (var falha in new[] { "conteudo", "permissao", "link" })
        {
            var c = new Cenario(raiz);
            var p = PermissaoLeituraAusente.Abrir(c.Backend);
            p.IniciarLeitura();
            if (falha == "conteudo") File.AppendAllText(c.Caminho, " ");
            if (falha == "permissao") File.SetUnixFileMode(c.Caminho, ArquivoPrivado | UnixFileMode.GroupRead);
            if (falha == "link")
            {
                File.Move(c.Caminho, c.Caminho + ".preservado");
                File.CreateSymbolicLink(c.Caminho, c.Caminho + ".preservado");
            }
            var t = new Terminal(HttpStatusCode.NotFound, "NoSuchKey");
            using var cliente = new FabricaHttpRetencao(p.ConferirTransporte).CriarClienteSintetico(t);
            await Negar(() => cliente.GetAsync(Endpoint + ManifestoRetencao.Bucket + "/" + p.Chave));
            Exigir(t.Chamadas == 0);
        }
        // Timeout/erro de transporte consomem a única tentativa; cancelamento prévio não envia.
        foreach (var cancelar in new[] { false, true })
        {
            var c = new Cenario(raiz);
            var p = PermissaoLeituraAusente.Abrir(c.Backend);
            p.IniciarLeitura();
            var t = new TerminalFalho(cancelar);
            using var cliente = new FabricaHttpRetencao(p.ConferirTransporte).CriarClienteSintetico(t);
            try { using var r = await cliente.GetAsync(Endpoint + ManifestoRetencao.Bucket + "/" + p.Chave); }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { }
            Exigir(t.Chamadas == 1 && p.Encaminhamentos == 1);
            await Negar(() => cliente.GetAsync(Endpoint + ManifestoRetencao.Bucket + "/" + p.Chave));
            Exigir(t.Chamadas == 1);
            c.ConferirIntacto();
        }
        {
            var c = new Cenario(raiz);
            var p = PermissaoLeituraAusente.Abrir(c.Backend);
            p.IniciarLeitura();
            var t = new Terminal(HttpStatusCode.NotFound, "NoSuchKey");
            using var cliente = new FabricaHttpRetencao(p.ConferirTransporte).CriarClienteSintetico(t);
            using var cancelado = new CancellationTokenSource();
            cancelado.Cancel();
            try { using var r = await cliente.GetAsync(Endpoint + ManifestoRetencao.Bucket + "/" + p.Chave, cancelado.Token); }
            catch (OperationCanceledException) { }
            Exigir(t.Chamadas == 0);
        }
        ProvarSerializadorReal(raiz);
        Console.WriteLine($"PASS leitura ausente OFFLINE: {negadas} negações; {classificacoes} classificações SDK sintéticas; GET único/corrida/imutabilidade. Rede=0; SQL=0; User Secrets=0; manifestos reais=0. Não prova R2.");
    }

    private static void ProvarSerializadorReal(string raiz)
    {
        var c = new Cenario(raiz);
        for (var i = 0; i < ManifestoRetencao.PastasAntigas.Length; i++)
        {
            var pasta = Path.Combine(c.Backend, "obj", ManifestoRetencao.PastasAntigas[i]);
            Directory.CreateDirectory(pasta, PastaPrivada);
            File.WriteAllBytes(Path.Combine(pasta, "evidencias.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                bucket = ManifestoRetencao.Bucket, catalogo = "locadora_" + Guid.NewGuid().ToString("N"),
                objetos = c.Antigas.Skip(i == 0 ? 0 : 1).Take(i == 0 ? 1 : 4).Select(chave => new
                {
                    Chave = chave, Mime = "image/png", TamanhoBytes = 3,
                    sha256 = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })),
                }),
            }));
        }
        var m = ManifestoRetencao.Criar(c.Backend, "locadora_" + Guid.NewGuid().ToString("N"), Endpoint, TimeProvider.System);
        var empresa = Guid.NewGuid();
        m.AutorizarEmpresa(empresa);
        for (var i = 0; i < 3; i++)
        {
            var agora = DateTimeOffset.UtcNow;
            var v = Veiculo.Novo(empresa, new("FIC1A01", "Sintética", "Azul", "Leitura offline",
                TipoCombustivel.Gasolina, 50, 2026, Guid.NewGuid()), agora);
            var f = FotoObjeto.Preparar(v, "image/png", 3, 1, 1,
                SHA256.HashData(new byte[] { 1, 2, 3 }), new byte[8], ProvedorFoto.R2,
                ManifestoRetencao.Bucket, Guid.NewGuid(), "qualificacao", agora);
            m.RegistrarIntencao(new(f.Chave, f.Mime, f.TamanhoBytes, f.Sha256));
            var t = TrabalhoObjeto.Criar(f, agora);
            var posse = t.Adquirir(agora);
            t.ConfirmarEnvio(posse, agora);
            f.Abandonar(agora);
            t.Concluir(posse, f, agora);
            m.Atualizar(f, t, true);
            // Atestado SOMENTE sintético, sem fingir HEAD no provedor.
            m.RegistrarAusencia(new(f.Chave, f.Mime, f.TamanhoBytes, f.Sha256));
        }
        var bytes = File.ReadAllBytes(m.Caminho);
        File.WriteAllBytes(c.Caminho, bytes);
        var p = PermissaoLeituraAusente.Abrir(c.Backend);
        p.IniciarLeitura();
        Exigir(File.ReadAllBytes(m.Caminho).SequenceEqual(bytes)
            && File.ReadAllBytes(c.Caminho).SequenceEqual(bytes));
        Console.WriteLine("PASS compatibilidade com serializador ManifestoRetencao real em árvore totalmente sintética.");
    }

    private static void Exigir(bool condicao) { if (!condicao) throw new Exception("Qualificação local não aprovada."); }

    private sealed class FabricaSintetica(PermissaoLeituraAusente p, Terminal terminal) : Amazon.Runtime.HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) =>
            new FabricaHttpRetencao(p.ConferirTransporte).CriarClienteSintetico(terminal);
    }

    private sealed class Terminal(HttpStatusCode status, string codigo) : HttpMessageHandler
    {
        private int chamadas;
        public int Chamadas => chamadas;
        public int Assinadas { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref chamadas);
            if (request.Headers.TryGetValues("Authorization", out var valores)
                && valores.Any(x => x.StartsWith("AWS4-HMAC-SHA256 ", StringComparison.Ordinal))) Assinadas++;
            var resposta = new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.OK ? "" :
                    $"<Error><Code>{codigo}</Code><Message>Erro sintético</Message></Error>"),
            };
            resposta.Content.Headers.ContentType = new("application/xml");
            if ((int)status is >= 300 and <= 399) resposta.Headers.Location = new("https://example.invalid/");
            return Task.FromResult(resposta);
        }
    }

    private sealed class TerminalFalho(bool cancelar) : HttpMessageHandler
    {
        public int Chamadas { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            return Task.FromException<HttpResponseMessage>(cancelar
                ? new OperationCanceledException("Timeout sintético") : new HttpRequestException("Falha sintética"));
        }
    }

    private sealed class Cenario
    {
        public string Backend { get; }
        public string Caminho { get; }
        public string[] Antigas { get; }
        public string[] Chaves { get; }
        private readonly byte[] original;
        public Cenario(string raiz)
        {
            Backend = Path.Combine(raiz, Guid.NewGuid().ToString("N"));
            var pasta = Path.Combine(Backend, "obj", PermissaoLeituraAusente.PastaAprovada);
            Directory.CreateDirectory(pasta, PastaPrivada);
            Caminho = Path.Combine(pasta, "evidencias.json");
            var empresa = Guid.NewGuid();
            Antigas = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid().ToString("N") + "/" + Guid.NewGuid().ToString("N")).ToArray();
            Chaves = Enumerable.Range(0, 3).Select(_ => empresa.ToString("N") + "/" + Guid.NewGuid().ToString("N")).ToArray();
            original = JsonSerializer.SerializeToUtf8Bytes(new
            {
                bucket = ManifestoRetencao.Bucket, endpoint = Endpoint.AbsoluteUri,
                catalogo = "locadora_" + Guid.NewGuid().ToString("N"),
                catalogosProtegidos = new[] { "locadora_" + Guid.NewGuid().ToString("N"), "locadora_" + Guid.NewGuid().ToString("N") },
                empresas = new[] { empresa }, denylist = Antigas,
                objetos = Chaves.Select(chave => new
                {
                    Chave = chave, Mime = "image/png", TamanhoBytes = 3,
                    Sha256 = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })),
                    Empresa = empresa, FotoId = Guid.ParseExact(chave.Split('/')[1], "N"),
                    TrabalhoId = Guid.NewGuid(), Estado = EstadoFoto.Inativa,
                    Tipo = TipoTrabalhoObjeto.RemocaoRetida, EstadoTrabalho = EstadoTrabalhoObjeto.Concluido,
                    SemReferenciasProtegidas = true, AusenciaConfirmadaEmUtc = DateTimeOffset.UtcNow,
                }),
            });
            File.WriteAllBytes(Caminho, original);
            File.SetUnixFileMode(Caminho, ArquivoPrivado);
        }
        public void ConferirIntacto() => Exigir(File.ReadAllBytes(Caminho).SequenceEqual(original)
            && File.GetUnixFileMode(Caminho) == ArquivoPrivado
            && File.GetUnixFileMode(Path.GetDirectoryName(Caminho)!) == PastaPrivada);
    }
}
