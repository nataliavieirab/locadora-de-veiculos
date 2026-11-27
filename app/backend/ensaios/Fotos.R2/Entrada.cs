using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Locadora.Ensaios.FotosR2;

// Fora da suíte automática: requer invocação explícita e jamais expurga objetos.
[SupportedOSPlatform("linux")]
internal static class Entrada
{
    private const string Bucket = "locadora-veiculos-dev";
    private static string etapa = "argumentos";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (OperatingSystem.IsLinux() && args is ["--validar-leitura-ausente"])
            {
                try
                {
                    await LeituraAusente.QualificacaoLeituraAusente.ExecutarAsync(LocalizarBackend());
                    return 0;
                }
                catch (Exception erro)
                {
                    // Exclusivo das fixtures sintéticas; nunca imprime Message/InnerException.
                    Console.Error.WriteLine("FAIL qualificação OFFLINE: " + erro.GetType().Name + erro.StackTrace);
                    return 1;
                }
            }
            if (OperatingSystem.IsLinux() && args is ["--executar-leitura-ausente-dev"])
                return await LeituraAusente.EntradaLeituraAusente.ExecutarAsync(LocalizarBackend());
            if (OperatingSystem.IsLinux() && args is ["--validar-retencao"])
            {
                await Retencao.QualificacaoRetencao.ExecutarAsync(LocalizarBackend());
                Console.WriteLine("PASS qualificação offline da retenção; zero rede e sem User Secrets. Não prova R2.");
                return 0;
            }
            if (OperatingSystem.IsLinux() && args is ["--executar-retencao-dev"])
                return await EntradaRetencao.ExecutarAsync(LocalizarBackend());
            // O modo histórico permanece no código como evidência, mas não pode
            // ser reexecutado com o protocolo revisado nem abrir credenciais.
            if (args is ["--executar-dev"])
                throw new InvalidOperationException();
            if (!OperatingSystem.IsLinux() || args.Length != 1 || args[0] is not ("--validar-protecao" or "--executar-dev"))
                throw new InvalidOperationException();

            // A qualificação offline não abre o armazenamento de segredos.
            if (args[0] == "--validar-protecao")
            {
                new ProtecaoR2(new Uri("https://00000000000000000000000000000000.r2.cloudflarestorage.com"), Bucket)
                    .ProvarBloqueios();
                Console.WriteLine("PASS proteção offline: sete destinos/métodos negados; zero requisições.");
                return 0;
            }

            etapa = "configuracao-local";
            var segredos = new ConfigurationBuilder().AddUserSecrets("locadora-de-veiculos-dev").Build();
            Exigir(segredos["Fotos:Provedor"] == "R2" && segredos["Fotos:R2:Bucket"] == Bucket
                && segredos.GetValue<bool>("Fotos:R2:BucketPrivadoConfirmado"));
            var endpoint = segredos["Fotos:R2:Endpoint"] ?? "";
            var configuracao = new ConfiguracaoR2
            {
                Endpoint = endpoint, Bucket = Bucket, BucketPrivadoConfirmado = true,
                AccessKey = segredos["Fotos:R2:AccessKey"] ?? "", SecretKey = segredos["Fotos:R2:SecretKey"] ?? "",
            };
            configuracao.Validar();
            var protecao = new ProtecaoR2(new Uri(endpoint), Bucket);
            protecao.ProvarBloqueios();
            var opcoes = configuracao.CriarOpcoes();
            opcoes.HttpClientFactory = new FabricaHttpProtegida(protecao);
            using var s3 = new AmazonS3Client(new BasicAWSCredentials(
                segredos["Fotos:R2:AccessKey"], segredos["Fotos:R2:SecretKey"]), opcoes);
            var objetos = new ObjetosProtegidos(new ArmazenamentoR2(s3, configuracao), protecao);

            var backend = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(backend.FullName, "Locadora.slnx")))
                backend = backend.Parent ?? throw new InvalidOperationException();
            var pasta = Path.Combine(backend.FullName, "obj", "r2-gate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pasta, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var manifesto = Path.Combine(pasta, "evidencias.json");
            var verificacoes = new List<string>();
            string? catalogo = null;
            void Registrar()
            {
                File.WriteAllText(manifesto, JsonSerializer.Serialize(new
                {
                    bucket = Bucket, catalogo, etapa, verificacoes,
                    requisicoesEncaminhadas = protecao.Enviadas,
                    bloqueiosTransporte = protecao.Bloqueadas,
                    deletesBloqueados = objetos.DeletesBloqueados,
                    objetos = objetos.Escritos.Select(o => new
                    {
                        o.Chave, o.Mime, o.TamanhoBytes, sha256 = Convert.ToHexString(o.Sha256),
                    }),
                    retencao = "Sem DELETE remoto; catálogo SQL e objetos preservados. Não constitui política de expurgo.",
                }, new JsonSerializerOptions { WriteIndented = true }));
                File.SetUnixFileMode(manifesto, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            objetos.RegistrarEvidencia = Registrar;
            Registrar();
            Console.WriteLine("Evidências privadas: " + Path.GetRelativePath(backend.FullName, manifesto));
            try
            {
                etapa = "catalogo-sql-sintetico";
                // Reusa o mecanismo de injeção SQL dos scripts oficiais.
                await AmbienteIntegracao.Iniciar(null!);
                catalogo = new SqlConnectionStringBuilder(AmbienteIntegracao.Conexao).InitialCatalog;
                Registrar();

                var atual = File.ReadAllLines("/proc/self/cgroup").Single(l => l.StartsWith("0::", StringComparison.Ordinal))[3..];
                var cgroups = Directory.GetParent(Path.Combine("/sys/fs/cgroup", atual.TrimStart('/')))!.FullName;
                var temporarios = Path.Combine(pasta, "temporarios");
                Directory.CreateDirectory(temporarios, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var processador = new ProcessadorImagemIsolado(new(
                    File.ResolveLinkTarget("/usr/bin/dotnet", true)!.FullName,
                    Path.Combine(backend.FullName, "src/Locadora.ProcessadorImagens/bin/Debug/net10.0/Locadora.ProcessadorImagens.dll"),
                    cgroups, temporarios));
                // Pré-qualificar imagem/processador antes de qualquer I/O remoto.
                var png = Imagem();
                var saneada = await processador.ProcessarAsync(new MemoryStream(png), "image/png", default);
                var outroPng = Imagem(outra: true);
                var outraSaneada = await processador.ProcessarAsync(new MemoryStream(outroPng), "image/png", default);
                verificacoes.Add("processador-real-cgroup");

                etapa = "host-http-isolado";
                await using var fabrica = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave,
                    extras: new()
                    {
                        ["Fotos:Provedor"] = "R2", ["Fotos:R2:Bucket"] = Bucket,
                        ["Limites:Registro:Quantidade"] = "100", ["Limites:Login:Quantidade"] = "100",
                    },
                    servicos: services =>
                    {
                        // Sem credenciais no ambiente global do FabricaApi; somente DI.
                        services.RemoveAll<IArmazenamentoObjeto>();
                        services.AddSingleton<IArmazenamentoObjeto>(objetos);
                        services.RemoveAll<IProcessadorImagem>();
                        services.AddSingleton<IProcessadorImagem>(processador);
                        services.AddLogging(logging => logging.ClearProviders());
                    },
                    configurar: builder => builder.UseContentRoot(Path.Combine(backend.FullName, "src/Locadora.Api")));
                using var a = await ClienteAsync(fabrica, protecao);
                using var b = await ClienteAsync(fabrica, protecao);
                using var anonimo = fabrica.CreateClient();
                var (id, etag) = await VeiculoAsync(a);

                etapa = "upload-e-leitura-r2";
                using var envio = await EnviarAsync(a, id, etag, png);
                Status(envio, 200);
                etag = envio.Headers.ETag!.ToString();
                using var leitura = await a.GetAsync($"/api/v1/veiculos/{id}/foto");
                Status(leitura, 200);
                Exigir(leitura.Headers.CacheControl is { Private: true, NoStore: true }
                    && leitura.Headers.Location is null
                    && leitura.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff"
                    && leitura.Content.Headers.ContentType?.MediaType == "image/png");
                Exigir((await leitura.Content.ReadAsByteArrayAsync()).SequenceEqual(saneada.Bytes));
                verificacoes.Add("put-get-reais-hash-mime-headers");

                etapa = "isolamento-e-anonimo";
                var antes = protecao.Enviadas;
                foreach (var (cliente, esperado) in new[] { (b, 404), (anonimo, 401) })
                {
                    using var get = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
                    Status(get, esperado);
                    using var put = await EnviarAsync(cliente, id, etag, png);
                    Status(put, esperado);
                    using var delete = await RemoverAsync(cliente, id, etag);
                    Status(delete, esperado);
                }
                Exigir(antes == protecao.Enviadas);
                using var externa = await a.PutAsJsonAsync($"/api/v1/veiculos/{id}/foto",
                    new { url = "https://example.invalid/sintetica.png" });
                Status(externa, 415);
                verificacoes.Add("tenant-b-anonimo-get-put-delete-negados-sem-io");

                etapa = "negacao-s3-anonima";
                using (var publico = protecao.CriarCliente())
                using (var negada = await publico.GetAsync(new Uri(new Uri(endpoint), Bucket + "/" + objetos.Escritos[0].Chave),
                    HttpCompletionOption.ResponseHeadersRead))
                {
                    // R2 observado retorna 400/InvalidArgument sem assinatura.
                    // Não converter erro de protocolo em prova de autorização.
                    var negacaoAutenticacao = negada.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
                    if (!negacaoAutenticacao)
                    {
                        Exigir(negada.StatusCode == HttpStatusCode.BadRequest);
                        using var leitor = new StreamReader(await negada.Content.ReadAsStreamAsync());
                        var buffer = new char[4096];
                        var lidos = await leitor.ReadBlockAsync(buffer);
                        var codigo = System.Xml.Linq.XDocument.Parse(new string(buffer, 0, lidos)).Root?.Element("Code")?.Value;
                        Exigir(codigo == "InvalidArgument");
                    }
                    verificacoes.Add(negacaoAutenticacao ? "s3-anonimo-nega-autenticacao"
                        : "s3-anonimo-400-invalidargument-nao-prova-privacidade-publica");
                }

                etapa = "substituicao-retida";
                using var troca = await EnviarAsync(a, id, etag, outroPng);
                Status(troca, 200);
                etag = troca.Headers.ETag!.ToString();
                Exigir(objetos.Escritos.Count == 2);
                Exigir((await objetos.LerAsync(objetos.Escritos[0], default)).SequenceEqual(saneada.Bytes));
                using var novaLeitura = await a.GetAsync($"/api/v1/veiculos/{id}/foto");
                Status(novaLeitura, 200);
                Exigir((await novaLeitura.Content.ReadAsByteArrayAsync()).SequenceEqual(outraSaneada.Bytes));
                verificacoes.Add("troca-conserva-objeto-anterior-r2");

                etapa = "falha-pos-put";
                objetos.FalharLeitura = true;
                using var falha = await EnviarAsync(a, id, etag, png);
                Status(falha, 503);
                using var leituraFalha = await a.GetAsync($"/api/v1/veiculos/{id}/foto");
                Status(leituraFalha, 503);
                using var detalhe = await a.GetAsync($"/api/v1/veiculos/{id}");
                Status(detalhe, 200);
                Exigir(detalhe.Headers.ETag!.ToString() == etag);
                objetos.FalharLeitura = false;
                using var preservada = await a.GetAsync($"/api/v1/veiculos/{id}/foto");
                Status(preservada, 200);
                Exigir((await preservada.Content.ReadAsByteArrayAsync()).SequenceEqual(outraSaneada.Bytes));
                verificacoes.Add("falha-leitura-injetada-apos-put-real-preserva-ativa-e-etag");

                etapa = "compensacao-bloqueada";
                using (var escopo = fabrica.Services.CreateScope())
                {
                    var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
                    var empresa = Guid.ParseExact(objetos.Escritos[2].Chave.Split('/')[0], "N");
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE TrabalhosObjetos SET LeaseAteUtc={DateTimeOffset.UtcNow.AddMinutes(-1)}, ProximoProcessamentoUtc={DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE EmpresaId={empresa} AND Tipo=0");
                    await escopo.ServiceProvider.GetRequiredService<IRetomadaFotos>().RetomarAsync(default);
                    var contexto = escopo.ServiceProvider.GetRequiredService<ContextoEmpresa>();
                    contexto.EmpresaId = empresa;
                    var trabalho = await db.TrabalhosObjetos.AsNoTracking().SingleAsync(t => t.Tipo == TipoTrabalhoObjeto.CompensarNuncaAtivo);
                    Exigir(trabalho.Estado == EstadoTrabalhoObjeto.Pendente && trabalho.Tentativas == 1 && trabalho.EnvioConfirmado);
                    Exigir(await db.FotosObjetos.CountAsync(f => f.Estado == EstadoFoto.Ativa) == 1);
                    Exigir(await db.FotosObjetos.CountAsync(f => f.Estado == EstadoFoto.Abandonada) == 1);
                }
                Exigir(objetos.DeletesBloqueados == 1);
                Exigir((await objetos.LerAsync(objetos.Escritos[2], default)).SequenceEqual(saneada.Bytes));
                verificacoes.Add("compensacao-duravel-backoff-delete-bloqueado-objeto-preservado");

                etapa = "remocao-logica";
                using var remocao = await RemoverAsync(a, id, etag);
                Status(remocao, 204);
                using var ausente = await a.GetAsync($"/api/v1/veiculos/{id}/foto");
                Status(ausente, 404);
                foreach (var objeto in objetos.Escritos)
                {
                    objeto.Conferir(await objetos.LerAsync(objeto, default));
                    Exigir(!await objetos.ConfirmarAusenciaAsync(objeto, default));
                }
                Exigir(objetos.DeletesBloqueados >= 1);
                verificacoes.Add("remocao-logica-404-tres-objetos-r2-preservados");

                etapa = "isolamento-inverso";
                var (idB, etagB) = await VeiculoAsync(b);
                using var envioB = await EnviarAsync(b, idB, etagB, png);
                Status(envioB, 200);
                etagB = envioB.Headers.ETag!.ToString();
                antes = protecao.Enviadas;
                using var getB = await a.GetAsync($"/api/v1/veiculos/{idB}/foto");
                Status(getB, 404);
                using var putB = await EnviarAsync(a, idB, etagB, png);
                Status(putB, 404);
                using var deleteB = await RemoverAsync(a, idB, etagB);
                Status(deleteB, 404);
                Exigir(antes == protecao.Enviadas && objetos.Escritos.Count == 4);
                verificacoes.Add("tenant-a-nao-le-altera-remove-foto-b-sem-io");

                etapa = "sessao-revogada";
                using var logout = await a.PostAsJsonAsync("/api/v1/session/logout", new { });
                Status(logout, 204);
                using var revogada = await a.GetAsync($"/api/v1/veiculos/{id}/foto");
                Status(revogada, 401);
                verificacoes.Add("logout-nega-foto");
                etapa = "concluido-parcial-sem-expurgo";
                Console.WriteLine($"PASS ensaio R2 protegido: {verificacoes.Count} verificações; quatro objetos conservados; zero DELETE remoto.");
                Console.WriteLine("Limites: falha de leitura injetada; sem prova de expurgo/compensação física ou domínios públicos no painel.");
                return 0;
            }
            finally
            {
                Registrar();
                // Não chamar AssemblyCleanup: preserva SQL e rastreabilidade dos objetos retidos.
                await AmbienteIntegracao.Fabrica.DisposeAsync();
            }
        }
        catch (Exception)
        {
            Console.Error.WriteLine("FAIL etapa fixa: " + etapa + ". Detalhes sensíveis suprimidos; não houve limpeza automática.");
            return 1;
        }
    }

    private static void Exigir(bool condicao)
    {
        if (!condicao) throw new InvalidOperationException();
    }
    private static string LocalizarBackend()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(pasta.FullName, "Locadora.slnx")))
            pasta = pasta.Parent ?? throw new InvalidOperationException();
        return pasta.FullName;
    }
    private static void Status(HttpResponseMessage resposta, int esperado) => Exigir((int)resposta.StatusCode == esperado);

    private static async Task<HttpClient> ClienteAsync(FabricaApi fabrica, ProtecaoR2 protecao)
    {
        var cliente = Clientes.Criar(fabrica);
        var usuario = "r2_" + Guid.NewGuid().ToString("N");
        using var registro = await ApiCliente.RegistrarAsync(cliente, usuario, "sintetico@example.invalid", "Senha123");
        Status(registro, 201);
        using var login = await ApiCliente.EntrarAsync(cliente, usuario, "Senha123");
        Status(login, 200);
        var token = (await ApiCliente.JsonAsync(login)).GetProperty("accessToken").GetString()!;
        protecao.Empresas.Add(ApiCliente.ClaimGuid(ApiCliente.LerToken(token), "empresaId").ToString("N"));
        ApiCliente.Bearer(cliente, token);
        return cliente;
    }

    private static async Task<(Guid Id, string Etag)> VeiculoAsync(HttpClient cliente)
    {
        using var grupo = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nome = "Grupo R2 sintético" });
        Status(grupo, 201);
        using var criado = await cliente.PostAsJsonAsync("/api/v1/veiculos", new
        {
            placa = "FIC1A01", marca = "Sintética", modelo = "Ensaio R2", cor = "Azul",
            ano = 2026, combustivel = "Gasolina", capacidadeLitros = "50",
            grupoId = (await ApiCliente.JsonAsync(grupo)).GetProperty("id").GetGuid(),
        });
        Status(criado, 201);
        return ((await ApiCliente.JsonAsync(criado)).GetProperty("id").GetGuid(), criado.Headers.ETag!.ToString());
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, Guid id, string etag, byte[] png)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/veiculos/{id}/foto");
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        var multipart = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(png);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(arquivo, "arquivo", "sintetico.png");
        pedido.Content = multipart;
        return await cliente.SendAsync(pedido);
    }

    private static async Task<HttpResponseMessage> RemoverAsync(HttpClient cliente, Guid id, string etag)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/veiculos/{id}/foto");
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        return await cliente.SendAsync(pedido);
    }

    private static byte[] Imagem(bool outra = false)
    {
        using var bitmap = new SKBitmap(32, 24);
        bitmap.Erase(outra ? SKColors.OrangeRed : SKColors.CornflowerBlue);
        using var imagem = SKImage.FromBitmap(bitmap);
        using var png = imagem.Encode(SKEncodedImageFormat.Png, 90);
        return png.ToArray();
    }
}
