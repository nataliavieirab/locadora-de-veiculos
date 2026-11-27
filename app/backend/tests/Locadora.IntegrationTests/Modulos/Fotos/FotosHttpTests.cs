using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Locadora.Api.Modulos.Fotos;
using Locadora.Application.Modulos.Fotos;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;
using Locadora.IntegrationTests.Suporte;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
public sealed class FotosHttpTests
{
    [TestMethod]
    public async Task VolumeInacessivelNaoImpedeConsultarOuCadastrarSemArquivo()
    {
        using var fabrica = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave,
            extras: new() { ["Fotos:Provedor"] = "Local", ["Fotos:RaizPrivada"] = "/diretorio-inexistente-f5" });
        using var cliente = await ClienteAsync(fabrica);
        var (id, _) = await VeiculoAsync(cliente);
        using var consulta = await cliente.GetAsync($"/api/v1/veiculos/{id}");
        Assert.AreEqual(200, (int)consulta.StatusCode);
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, (await ApiCliente.JsonAsync(consulta)).GetProperty("foto").ValueKind);
        // Evita deixar configuração sintética global para as fábricas seguintes.
        Environment.SetEnvironmentVariable("Fotos__Provedor", null);
        Environment.SetEnvironmentVariable("Fotos__RaizPrivada", null);
    }

    [TestMethod]
    public async Task CancelamentoHttpChegaAoStorageSemEntregarImagem()
    {
        var objetos = new Objetos();
        using var fabrica = Fabrica(objetos);
        using var cliente = await ClienteAsync(fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        using var envio = await EnviarAsync(cliente, id, etag);
        var iniciou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        objetos.DuranteLeitura = async token =>
        {
            iniciou.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { terminou.SetResult(); }
        };
        using var cancelar = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var leitura = cliente.GetAsync($"/api/v1/veiculos/{id}/foto", cancelar.Token);
        await iniciou.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancelar.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => { using var resposta = await leitura; });
        await terminou.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task OcupacaoNegaMutacoesEPendenciaOuVeiculoExcluidoNaoOferecemBytes()
    {
        var objetos = new Objetos { Falhar = true };
        var ocupacao = new Ocupacao();
        using var fabrica = Fabrica(objetos, ocupacao: ocupacao);
        using var cliente = await ClienteAsync(fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        using var falha = await EnviarAsync(cliente, id, etag);
        Assert.AreEqual(503, (int)falha.StatusCode);
        objetos.Falhar = false;
        using var pendente = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
        Assert.AreEqual(404, (int)pendente.StatusCode);
        using var envio = await EnviarAsync(cliente, id, etag);
        etag = envio.Headers.ETag!.ToString();
        ocupacao.Aberto = true;
        using var envioOcupado = await EnviarAsync(cliente, id, etag);
        using var remocaoOcupada = await RemoverAsync(cliente, id, etag);
        Assert.AreEqual(409, (int)envioOcupado.StatusCode);
        Assert.AreEqual(409, (int)remocaoOcupada.StatusCode);
        using var detalhe = await cliente.GetAsync($"/api/v1/veiculos/{id}");
        Assert.IsTrue((await ApiCliente.JsonAsync(detalhe)).GetProperty("possuiAluguelAberto").GetBoolean());
        using var consulta = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
        Assert.AreEqual(200, (int)consulta.StatusCode);
        ocupacao.Aberto = false;
        using var excluir = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/veiculos/{id}");
        excluir.Headers.TryAddWithoutValidation("If-Match", etag);
        using var excluido = await cliente.SendAsync(excluir);
        Assert.AreEqual(204, (int)excluido.StatusCode);
        using var leitura = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
        Assert.AreEqual(404, (int)leitura.StatusCode);
    }

    [TestMethod]
    public async Task ContratoAtivaLeComHeadersPrivadosTrocaRemoveEConservaVersaoSemReferencia()
    {
        var objetos = new Objetos();
        using var fabrica = Fabrica(objetos);
        using var cliente = await ClienteAsync(fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        using var detalhe = await cliente.GetAsync($"/api/v1/veiculos/{id}");
        var json = await ApiCliente.JsonAsync(detalhe);
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, json.GetProperty("foto").ValueKind);
        Assert.IsFalse(json.GetProperty("possuiAluguelAberto").GetBoolean());
        using var envio = await EnviarAsync(cliente, id, etag);
        Assert.AreEqual(HttpStatusCode.OK, envio.StatusCode, await envio.Content.ReadAsStringAsync());
        var novaVersao = envio.Headers.ETag!.ToString();
        Assert.AreNotEqual(etag, novaVersao);
        var foto = (await ApiCliente.JsonAsync(envio)).GetProperty("foto");
        Assert.AreEqual($"/api/v1/veiculos/{id}/foto", foto.GetProperty("caminho").GetString());
        Assert.AreEqual(6, foto.EnumerateObject().Count());
        using var pedido = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/veiculos/{id}/foto");
        pedido.Headers.TryAddWithoutValidation("If-None-Match", "*");
        pedido.Headers.TryAddWithoutValidation("Range", "bytes=0-0");
        using var leitura = await cliente.SendAsync(pedido);
        Assert.AreEqual(HttpStatusCode.OK, leitura.StatusCode);
        Assert.IsTrue(leitura.Headers.CacheControl!.Private && leitura.Headers.CacheControl.NoStore);
        Assert.AreEqual("nosniff", leitura.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.AreEqual("image/png", leitura.Content.Headers.ContentType!.MediaType);
        Assert.IsNull(leitura.Content.Headers.ContentDisposition);
        Assert.AreEqual(3L, leitura.Content.Headers.ContentLength);
        Assert.IsNull(leitura.Headers.Location);
        Assert.IsNull(leitura.Content.Headers.ContentRange);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await leitura.Content.ReadAsByteArrayAsync());
        using var troca = await EnviarAsync(cliente, id, novaVersao);
        Assert.AreEqual(HttpStatusCode.OK, troca.StatusCode);
        Assert.AreNotEqual(foto.GetProperty("id").GetGuid(), (await ApiCliente.JsonAsync(troca)).GetProperty("foto").GetProperty("id").GetGuid());
        using var removida = await RemoverAsync(cliente, id, troca.Headers.ETag!.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, removida.StatusCode);
        using var ausente = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
        Assert.AreEqual(HttpStatusCode.NotFound, ausente.StatusCode);
        using var repetida = await RemoverAsync(cliente, id, removida.Headers.ETag!.ToString());
        Assert.AreEqual(removida.Headers.ETag, repetida.Headers.ETag);
        Assert.AreEqual(2, objetos.Dados.Count); // retenção, nunca DELETE de ativo
    }

    [TestMethod]
    public async Task ValidaMultipartLimitesErrosEAutorizacaoAntesDoProcessador()
    {
        var objetos = new Objetos();
        var processador = new Processador();
        using var fabrica = Fabrica(objetos, processador,
            destino: new(ProvedorFoto.Local, "http-" + Guid.NewGuid().ToString("N")), retomar: true);
        using var cliente = await ClienteAsync(fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        using var semVersao = await EnviarAsync(cliente, id, null);
        Assert.AreEqual(428, (int)semVersao.StatusCode);
        using var obsoleta = await EnviarAsync(cliente, id, "\"AAAAAAAAAAA=\"");
        Assert.AreEqual(412, (int)obsoleta.StatusCode);
        Assert.AreEqual(0, processador.Chamadas);
        foreach (var (bytes, nome, mime, extra, esperado) in new[]
        {
            (Array.Empty<byte>(), "arquivo", "image/png", false, 400),
            (new byte[] { 1 }, "url", "image/png", false, 400),
            (new byte[] { 1 }, "arquivo", "image/gif", false, 415),
            (new byte[] { 1 }, "arquivo", "image/png", true, 400),
            (new byte[5242881], "arquivo", "image/png", false, 413),
        })
        {
            using var resposta = await EnviarAsync(cliente, id, etag, bytes, nome, mime, extra);
            Assert.AreEqual(esperado, (int)resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
            Assert.IsTrue((await ApiCliente.JsonAsync(resposta)).GetProperty("errors").TryGetProperty("arquivo", out _));
        }
        using var urlExterna = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/veiculos/{id}/foto");
        urlExterna.Headers.TryAddWithoutValidation("If-Match", etag);
        urlExterna.Content = JsonContent.Create(new { url = "https://example.invalid/sintetica.png" });
        using var urlNegada = await cliente.SendAsync(urlExterna);
        Assert.AreEqual(415, (int)urlNegada.StatusCode);
        Assert.AreEqual(0, objetos.Chamadas, "Multipart inválido não deve alcançar armazenamento.");
        Assert.AreEqual(0, processador.Chamadas, "Multipart inválido não deve alcançar o processador.");
        using var limite = await EnviarAsync(cliente, id, etag, new byte[5242880]);
        Assert.AreEqual(HttpStatusCode.OK, limite.StatusCode);
        etag = limite.Headers.ETag!.ToString();
        foreach (var falha in Enum.GetValues<FalhaProcessamentoImagem>())
        {
            processador.Falha = falha;
            using var resposta = await EnviarAsync(cliente, id, etag);
            Assert.AreEqual(falha switch
            { FalhaProcessamentoImagem.Formato => 415, FalhaProcessamentoImagem.Excesso => 413,
                FalhaProcessamentoImagem.Invalida => 400, _ => 503 }, (int)resposta.StatusCode);
        }
        processador.Falha = null;
        objetos.Falhar = true;
        using var falhaStorage = await EnviarAsync(cliente, id, etag);
        Assert.AreEqual(503, (int)falhaStorage.StatusCode);
        using var leituraFalha = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
        Assert.AreEqual(503, (int)leituraFalha.StatusCode);
        using var detalhe = await cliente.GetAsync($"/api/v1/veiculos/{id}");
        Assert.AreEqual(HttpStatusCode.OK, detalhe.StatusCode);
        Assert.AreNotEqual(System.Text.Json.JsonValueKind.Null, (await ApiCliente.JsonAsync(detalhe)).GetProperty("foto").ValueKind);
    }

    [TestMethod]
    public async Task CorpoInteiroTemTetoInclusiveSemContentLengthERejeitaPartesExtras()
    {
        using var fabrica = Fabrica(new Objetos());
        using var cliente = await ClienteAsync(fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        const string cabecalho = "--b\r\nContent-Disposition: form-data; name=\"arquivo\"; filename=\"ignorado\"\r\nContent-Type: image/png\r\n\r\n";
        const string fim = "\r\n--b--\r\n";
        foreach (var exceder in new[] { false, true })
        {
            var corpo = Encoding.ASCII.GetBytes(cabecalho + new string('x', 5242880) + fim);
            Array.Resize(ref corpo, LeitorMultipartFoto.MaximoCorpo + (exceder ? 1 : 0));
            using var pedido = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/veiculos/{id}/foto");
            pedido.Headers.TryAddWithoutValidation("If-Match", etag);
            pedido.Content = new CorpoSemTamanho(corpo);
            pedido.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=b");
            using var resposta = await cliente.SendAsync(pedido);
            Assert.AreEqual(exceder ? 413 : 200, (int)resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
            if (!exceder) etag = resposta.Headers.ETag!.ToString();
        }
        using var truncado = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/veiculos/{id}/foto");
        truncado.Headers.TryAddWithoutValidation("If-Match", etag);
        truncado.Content = new StringContent(cabecalho + "sem-delimitador-final");
        truncado.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=b");
        using var invalido = await cliente.SendAsync(truncado);
        Assert.AreEqual(400, (int)invalido.StatusCode);
        Assert.IsTrue((await ApiCliente.JsonAsync(invalido)).GetProperty("errors").TryGetProperty("arquivo", out _));
    }

    [TestMethod]
    public async Task LeituraRevalidaSessaoEReferenciaAposIoENegaTenantExcluidoEAnonimo()
    {
        var objetos = new Objetos();
        using var fabrica = Fabrica(objetos,
            destino: new(ProvedorFoto.Local, "http-" + Guid.NewGuid().ToString("N")), retomar: true);
        using var cliente = await ClienteAsync(fabrica);
        using var externo = await ClienteAsync(fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        using var envio = await EnviarAsync(cliente, id, etag);
        etag = envio.Headers.ETag!.ToString();
        var (idExterno, versaoExterna) = await VeiculoAsync(externo);
        using var envioExterno = await EnviarAsync(externo, idExterno, versaoExterna);
        foreach (var (solicitante, alvo, versao) in new[]
        {
            (externo, id, etag), (cliente, idExterno, envioExterno.Headers.ETag!.ToString()),
        })
        {
            await ConferirNegativasSemIoAsync(solicitante, alvo, versao, objetos, 404);
        }
        using var anonimo = fabrica.CreateClient();
        await ConferirNegativasSemIoAsync(anonimo, id, etag, objetos, 401);
        objetos.AposLer = async () =>
        {
            using var removida = await RemoverAsync(cliente, id, etag);
            Assert.AreEqual(204, (int)removida.StatusCode);
        };
        using var retiradaDuranteLeitura = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
        Assert.AreEqual(404, (int)retiradaDuranteLeitura.StatusCode);
        objetos.AposLer = null;
        using var detalhe = await cliente.GetAsync($"/api/v1/veiculos/{id}");
        using var novoEnvio = await EnviarAsync(cliente, id, detalhe.Headers.ETag!.ToString());
        objetos.AposLer = async () => { using var logout = await cliente.PostAsJsonAsync("/api/v1/session/logout", new { }); };
        using var revogada = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
        Assert.AreEqual(401, (int)revogada.StatusCode);
        Assert.AreEqual(0, (await revogada.Content.ReadAsStringAsync()).Contains("Chave", StringComparison.Ordinal) ? 1 : 0);
        objetos.AposLer = null;
        await ConferirNegativasSemIoAsync(cliente, id, novoEnvio.Headers.ETag!.ToString(), objetos, 401);
        using var preservada = await externo.GetAsync($"/api/v1/veiculos/{idExterno}/foto");
        Assert.AreEqual(200, (int)preservada.StatusCode);
    }

    private static async Task ConferirNegativasSemIoAsync(HttpClient cliente, Guid id, string etag,
        Objetos objetos, int esperado)
    {
        var antes = objetos.Chamadas;
        foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Delete })
        {
            using var pedido = new HttpRequestMessage(metodo, $"/api/v1/veiculos/{id}/foto");
            pedido.Headers.TryAddWithoutValidation("If-Match", etag);
            if (metodo == HttpMethod.Put) pedido.Content = Multipart([1]);
            using var resposta = await cliente.SendAsync(pedido);
            Assert.AreEqual(esperado, (int)resposta.StatusCode);
            Assert.IsNull(resposta.Headers.Location);
        }
        // Retomada real habilitada; este destino não tem trabalhos elegíveis.
        // Não generalizar para I/O técnico de trabalhos independentes já vencidos.
        Assert.AreEqual(antes, objetos.Chamadas, "A negativa não deve acessar os objetos solicitados.");
    }

    [TestMethod]
    public async Task PoliticaNegadaRetorna403EOpenApiDocumentaCampoTetosEPrecondicao()
    {
        using var fabrica = Fabrica(new Objetos(), negar: true);
        using var cliente = await ClienteAsync(fabrica);
        using var negada = await cliente.GetAsync($"/api/v1/veiculos/{Guid.NewGuid()}/foto");
        Assert.AreEqual(403, (int)negada.StatusCode);
        using var documento = await cliente.GetAsync("/openapi/v1.json");
        var json = await ApiCliente.JsonAsync(documento);
        var put = json.GetProperty("paths").GetProperty("/api/v1/veiculos/{id}/foto").GetProperty("put");
        Assert.IsTrue(put.GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
        Assert.IsTrue(put.GetProperty("requestBody").GetProperty("description").GetString()!.Contains("5.308.416", StringComparison.Ordinal));
        Assert.IsTrue(put.GetProperty("parameters").EnumerateArray().Any(p => p.GetProperty("name").GetString() == "If-Match"));
    }

    internal static FabricaApi Fabrica(IArmazenamentoObjeto objetos, Processador? processador = null,
        bool negar = false, Ocupacao? ocupacao = null, DestinoFotos? destino = null,
        TimeProvider? tempo = null, bool retomar = false) =>
        new(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave, servicos: servicos =>
        {
            servicos.RemoveAll<IRetomadaFotos>();
            if (retomar) servicos.AddSingleton<IRetomadaFotos, RetomadaFotos>();
            else servicos.AddSingleton<IRetomadaFotos, RetomadaNula>();
            if (destino is not null)
            {
                servicos.RemoveAll<DestinoFotos>();
                servicos.AddSingleton(destino);
            }
            if (tempo is not null)
            {
                servicos.RemoveAll<TimeProvider>();
                servicos.AddSingleton(tempo);
            }
            servicos.RemoveAll<IArmazenamentoObjeto>();
            servicos.AddSingleton<IArmazenamentoObjeto>(objetos);
            servicos.RemoveAll<IProcessadorImagem>();
            servicos.AddSingleton<IProcessadorImagem>(processador ?? new Processador());
            if (ocupacao is not null)
            {
                servicos.RemoveAll<IVinculosAluguelVeiculo>();
                servicos.AddSingleton<IVinculosAluguelVeiculo>(ocupacao);
            }
            if (negar) servicos.AddAuthorization(o => o.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().RequireClaim("politica_sintetica").Build());
        });

    internal static async Task<HttpClient> ClienteAsync(FabricaApi fabrica)
    {
        var cliente = fabrica.CreateClient();
        var usuario = "foto_" + Guid.NewGuid().ToString("N");
        using var registro = await ApiCliente.RegistrarAsync(cliente, usuario, "foto@example.invalid", "Senha123");
        Assert.AreEqual(201, (int)registro.StatusCode);
        using var login = await ApiCliente.EntrarAsync(cliente, usuario, "Senha123");
        ApiCliente.Bearer(cliente, (await ApiCliente.JsonAsync(login)).GetProperty("accessToken").GetString()!);
        return cliente;
    }

    internal static async Task<(Guid Id, string Etag)> VeiculoAsync(HttpClient cliente)
    {
        using var grupo = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nome = "Grupo foto" });
        var grupoId = (await ApiCliente.JsonAsync(grupo)).GetProperty("id").GetGuid();
        using var criado = await cliente.PostAsJsonAsync("/api/v1/veiculos", new
        {
            placa = "FIC1A01", marca = "Marca", modelo = "Horizonte", cor = "Prata",
            ano = 2026, combustivel = "Gasolina", capacidadeLitros = "50", grupoId,
        });
        Assert.AreEqual(201, (int)criado.StatusCode);
        return ((await ApiCliente.JsonAsync(criado)).GetProperty("id").GetGuid(), criado.Headers.ETag!.ToString());
    }

    internal static MultipartFormDataContent Multipart(byte[] bytes, string nome = "arquivo", string mime = "image/png", bool extra = false)
    {
        var conteudo = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(bytes);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue(mime);
        conteudo.Add(arquivo, nome, "../../ignorado.png");
        if (extra) conteudo.Add(new StringContent("https://example.invalid"), "url");
        return conteudo;
    }

    internal static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, Guid id, string? etag,
        byte[]? bytes = null, string nome = "arquivo", string mime = "image/png", bool extra = false)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/veiculos/{id}/foto");
        if (etag is not null) pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        pedido.Content = Multipart(bytes ?? [1], nome, mime, extra);
        return await cliente.SendAsync(pedido);
    }

    internal static async Task<HttpResponseMessage> RemoverAsync(HttpClient cliente, Guid id, string etag)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/veiculos/{id}/foto");
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        return await cliente.SendAsync(pedido);
    }

    internal sealed class Processador : IProcessadorImagem
    {
        public int Chamadas { get; private set; }
        public FalhaProcessamentoImagem? Falha { get; set; }
        public Task<ImagemSaneada> ProcessarAsync(Stream arquivo, string mime, CancellationToken cancelamento)
        {
            Chamadas++;
            if (Falha is { } falha) throw new ProcessamentoImagemException(falha);
            return Task.FromResult(new ImagemSaneada([1, 2, 3], mime, 1, 1));
        }
    }

    private sealed class Objetos : IArmazenamentoObjeto
    {
        public Dictionary<string, byte[]> Dados { get; } = [];
        public int Chamadas { get; private set; }
        public bool Falhar { get; set; }
        public Func<Task>? AposLer { get; set; }
        public Func<CancellationToken, Task>? DuranteLeitura { get; set; }
        public Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken cancelamento)
        {
            Chamadas++;
            if (Falhar) throw new ArmazenamentoObjetoException();
            Dados[objeto.Chave] = bytes.ToArray();
            return Task.CompletedTask;
        }
        public async Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken cancelamento)
        {
            Chamadas++;
            if (Falhar) throw new ArmazenamentoObjetoException();
            var bytes = Dados[objeto.Chave];
            if (DuranteLeitura is not null) await DuranteLeitura(cancelamento);
            if (AposLer is not null) await AposLer();
            return bytes;
        }
        public Task RemoverAsync(ObjetoFoto objeto, CancellationToken cancelamento)
        { Chamadas++; throw new AssertFailedException("Não apagar ativo."); }
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken cancelamento)
        { Chamadas++; return Task.FromResult(false); }
    }

    private sealed class RetomadaNula : IRetomadaFotos
    {
        public Task RetomarAsync(CancellationToken cancelamento) => Task.CompletedTask;
    }

    internal sealed class Ocupacao : IVinculosAluguelVeiculo
    {
        public bool Aberto { get; set; }
        public Task<bool> PossuiAluguelAbertoAsync(Guid veiculoId, CancellationToken cancelamento) => Task.FromResult(Aberto);
    }

    private sealed class CorpoSemTamanho(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }
}
