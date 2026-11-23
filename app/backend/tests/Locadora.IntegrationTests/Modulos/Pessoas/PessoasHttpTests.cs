using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Locadora.IntegrationTests.Modulos.Pessoas;

[TestClass]
public sealed class PessoasHttpTests
{
    [TestMethod]
    public async Task RotasDePessoasExigemSessao()
    {
        using var cliente = Clientes.Criar();
        foreach (var caminho in new[] { "/api/v1/clientes", "/api/v1/condutores", $"/api/v1/clientes/{Guid.NewGuid()}/vinculos-pj" })
        {
            using var resposta = await cliente.GetAsync(caminho);
            Assert.AreEqual(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }
    }

    [TestMethod]
    public async Task PfEPjTemContratosDiscriminadosLocationEtagEPaginacao()
    {
        using var cliente = await AutenticadoAsync();
        var pf = await CriarAsync(cliente, "/api/v1/clientes", Pf());
        var pj = await CriarAsync(cliente, "/api/v1/clientes", Pj());
        Assert.AreEqual("PF", pf.Json.GetProperty("tipo").GetString());
        Assert.IsTrue(pf.Json.TryGetProperty("cpf", out _));
        Assert.IsFalse(pf.Json.TryGetProperty("cnpj", out _));
        Assert.AreEqual("PJ", pj.Json.GetProperty("tipo").GetString());
        Assert.IsFalse(pj.Json.TryGetProperty("cpf", out _));
        Assert.IsFalse(pj.Json.TryGetProperty("cnh", out _));
        Assert.AreEqual(JsonValueKind.Null, pj.Json.GetProperty("email").ValueKind);
        using var lista = await cliente.GetAsync("/api/v1/clientes?page=1&pageSize=1");
        var pagina = await ApiCliente.JsonAsync(lista);
        Assert.AreEqual(2, pagina.GetProperty("total").GetInt32());
        Assert.AreEqual(1, pagina.GetProperty("items").GetArrayLength());
        Assert.AreEqual(1, pagina.GetProperty("page").GetInt32());
        Assert.AreEqual(1, pagina.GetProperty("pageSize").GetInt32());
        Assert.IsFalse(pagina.GetProperty("items")[0].TryGetProperty("endereco", out _));
        using var detalhe = await cliente.GetAsync($"/api/v1/clientes/{pf.Id}");
        Assert.AreEqual(pf.Etag, detalhe.Headers.ETag!.ToString());
        Assert.IsTrue(ApiCliente.NoStore(detalhe));
        ApiCliente.SemCookie(detalhe);
    }

    [TestMethod]
    public async Task ExclusaoBloqueiaCondutorAtivoEEncerraVinculosAposRetiradaDoCondutor()
    {
        using var cliente = await AutenticadoAsync();
        var pf = await CriarAsync(cliente, "/api/v1/clientes", Pf());
        var pj = await CriarAsync(cliente, "/api/v1/clientes", Pj());
        var vinculo = await CriarAsync(cliente, $"/api/v1/clientes/{pf.Id}/vinculos-pj", new { outroClienteId = pj.Id });
        var condutor = await CriarAsync(cliente, "/api/v1/condutores", Condutor(pf.Id));
        Assert.AreEqual("Pessoa sintética", condutor.Json.GetProperty("clienteNome").GetString());
        using var bloqueada = await MutarAsync(cliente, HttpMethod.Delete, $"/api/v1/clientes/{pf.Id}", pf.Etag);
        await ProblemaAsync(bloqueada, 409, "cliente_com_condutores_ativos");
        using var leituraVinculo = await cliente.GetAsync($"/api/v1/clientes/{pf.Id}/vinculos-pj/{vinculo.Id}");
        Assert.AreEqual(HttpStatusCode.OK, leituraVinculo.StatusCode);

        using var excluiCondutor = await MutarAsync(cliente, HttpMethod.Delete, $"/api/v1/condutores/{condutor.Id}", condutor.Etag);
        Assert.AreEqual(HttpStatusCode.NoContent, excluiCondutor.StatusCode);
        using var excluiCliente = await MutarAsync(cliente, HttpMethod.Delete, $"/api/v1/clientes/{pf.Id}", pf.Etag);
        Assert.AreEqual(HttpStatusCode.NoContent, excluiCliente.StatusCode);
        using var vinculos = await cliente.GetAsync($"/api/v1/clientes/{pj.Id}/vinculos-pj");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(vinculos)).GetArrayLength());
        using var outroCliente = await cliente.GetAsync($"/api/v1/clientes/{pj.Id}");
        Assert.AreEqual(HttpStatusCode.OK, outroCliente.StatusCode);
        using var excluido = await cliente.GetAsync($"/api/v1/clientes/{pf.Id}");
        await ProblemaAsync(excluido, 404, "cadastro_nao_encontrado");
    }

    [TestMethod]
    public async Task EdicaoExigeIfMatchERejeitaVersaoAntigaEDocumentoDuplicado()
    {
        using var cliente = await AutenticadoAsync();
        var pf = await CriarAsync(cliente, "/api/v1/clientes", Pf());
        using var ausente = await cliente.PutAsJsonAsync($"/api/v1/clientes/{pf.Id}", Pf("Nome atualizado"));
        await ProblemaAsync(ausente, 428, "versao_ausente");
        using var alteracao = await MutarAsync(cliente, HttpMethod.Put, $"/api/v1/clientes/{pf.Id}", pf.Etag, Pf("Nome atualizado"));
        Assert.AreEqual(HttpStatusCode.OK, alteracao.StatusCode);
        Assert.AreNotEqual(pf.Etag, alteracao.Headers.ETag!.ToString());
        using var antiga = await MutarAsync(cliente, HttpMethod.Put, $"/api/v1/clientes/{pf.Id}", pf.Etag, Pf());
        await ProblemaAsync(antiga, 412, "versao_divergente");
        using var duplicada = await cliente.PostAsJsonAsync("/api/v1/clientes", Pf());
        await ProblemaAsync(duplicada, 409, "cadastro_duplicado");
        var json = await ApiCliente.JsonAsync(duplicada);
        Assert.IsTrue(json.GetProperty("errors").TryGetProperty("cpf", out _));
        Assert.DoesNotContain(await duplicada.Content.ReadAsStringAsync(), "123.000.000-32");
    }

    [TestMethod]
    public async Task EmpresasNaoConsultamAlteramNemVinculamRecursosExternos()
    {
        using var a = await AutenticadoAsync();
        using var b = await AutenticadoAsync();
        var pfA = await CriarAsync(a, "/api/v1/clientes", Pf());
        var pfB = await CriarAsync(b, "/api/v1/clientes", Pf());
        var pjB = await CriarAsync(b, "/api/v1/clientes", Pj());
        var condutorB = await CriarAsync(b, "/api/v1/condutores", Condutor(pfB.Id));
        var vinculoB = await CriarAsync(b, $"/api/v1/clientes/{pfB.Id}/vinculos-pj", new { outroClienteId = pjB.Id });
        foreach (var caminho in new[] { $"/api/v1/clientes/{pfB.Id}", $"/api/v1/condutores/{condutorB.Id}",
            $"/api/v1/clientes/{pfA.Id}/vinculos-pj/{vinculoB.Id}" })
        {
            using var resposta = await a.GetAsync(caminho);
            await ProblemaAsync(resposta, 404, "cadastro_nao_encontrado");
        }
        using var escrita = await MutarAsync(a, HttpMethod.Put, $"/api/v1/clientes/{pfB.Id}", pfB.Etag, Pf("Tentativa externa"));
        await ProblemaAsync(escrita, 404, "cadastro_nao_encontrado");
        using var associacao = await a.PostAsJsonAsync($"/api/v1/clientes/{pfA.Id}/vinculos-pj", new { outroClienteId = pjB.Id });
        await ProblemaAsync(associacao, 404, "cadastro_nao_encontrado");
        using var condutor = await a.PostAsJsonAsync("/api/v1/condutores", Condutor(pfB.Id));
        await ProblemaAsync(condutor, 404, "cadastro_nao_encontrado");
        using var lista = await a.GetAsync("/api/v1/clientes");
        Assert.AreEqual(1, (await ApiCliente.JsonAsync(lista)).GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task FormatoDocumentoEDiscriminadorInvalidosNaoVazamDadosEmRespostasOuLogs()
    {
        var logs = new ColetorLogs();
        await using var fabrica = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave,
            servicos: servicos => servicos.AddLogging(logging => logging.AddProvider(logs)));
        using var cliente = await AutenticadoAsync(fabrica);
        const string documento = "123.000.000-33";
        using var invalido = await cliente.PostAsJsonAsync("/api/v1/clientes", Pf(cpf: documento));
        await ProblemaAsync(invalido, 400, "cadastro_invalido");
        using var discriminador = await cliente.PostAsJsonAsync("/api/v1/clientes", new { tipo = documento });
        await ProblemaAsync(discriminador, 400, "requisicao_invalida");
        using var pagina = await cliente.GetAsync("/api/v1/clientes?page=12300000033");
        await ProblemaAsync(pagina, 400, "requisicao_invalida");
        foreach (var resposta in new[] { invalido, discriminador, pagina })
        {
            var texto = await resposta.Content.ReadAsStringAsync();
            Assert.DoesNotContain(texto, documento);
            Assert.DoesNotContain(texto, "12300000033");
            Assert.DoesNotContain(texto, "JsonException");
        }
        var mensagens = string.Join('\n', logs.Mensagens);
        Assert.DoesNotContain(mensagens, documento);
        Assert.DoesNotContain(mensagens, "12300000033");
    }

    [TestMethod]
    public async Task CamposObrigatoriosEModalidadeIncompativelGeramErrosPorCampoSemGravar()
    {
        using var cliente = await AutenticadoAsync();
        using var invalido = await cliente.PostAsJsonAsync("/api/v1/clientes", new { tipo = "PF", nome = "" });
        await ProblemaAsync(invalido, 400, "requisicao_invalida");
        var erros = (await ApiCliente.JsonAsync(invalido)).GetProperty("errors");
        Assert.IsTrue(erros.TryGetProperty("nome", out _));
        Assert.IsTrue(erros.TryGetProperty("cpf", out _));
        using var pjComCpf = await cliente.PostAsJsonAsync("/api/v1/clientes", new
        {
            tipo = "PJ", nome = "PJ sintética", cnpj = "12.ABC.345/01DE-35", cpf = "123.000.000-32",
            telefone = "000000000", endereco = Endereco(),
        });
        await ProblemaAsync(pjComCpf, 400, "requisicao_invalida");
        using var lista = await cliente.GetAsync("/api/v1/clientes");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(lista)).GetProperty("total").GetInt32());
    }

    private static async Task<HttpClient> AutenticadoAsync(FabricaApi? fabrica = null)
    {
        var cliente = Clientes.Criar(fabrica);
        var username = "cadastros_" + Guid.NewGuid().ToString("N");
        using var registro = await ApiCliente.RegistrarAsync(cliente, username, "teste@example.invalid", "Senha123");
        Assert.AreEqual(HttpStatusCode.Created, registro.StatusCode);
        using var login = await ApiCliente.EntrarAsync(cliente, username, "Senha123");
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var json = await ApiCliente.JsonAsync(login);
        ApiCliente.Bearer(cliente, json.GetProperty("accessToken").GetString()!);
        return cliente;
    }

    private static async Task<(Guid Id, string Etag, JsonElement Json)> CriarAsync(HttpClient cliente, string caminho, object dados)
    {
        using var resposta = await cliente.PostAsJsonAsync(caminho, dados);
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        Assert.IsNotNull(resposta.Headers.Location);
        Assert.IsNotNull(resposta.Headers.ETag);
        var json = await ApiCliente.JsonAsync(resposta);
        var id = json.GetProperty("id").GetGuid();
        Assert.IsTrue(resposta.Headers.Location.ToString().EndsWith(id.ToString(), StringComparison.Ordinal));
        return (id, resposta.Headers.ETag.ToString(), json);
    }

    private static async Task<HttpResponseMessage> MutarAsync(HttpClient cliente, HttpMethod metodo, string caminho, string etag, object? corpo = null)
    {
        using var pedido = new HttpRequestMessage(metodo, caminho);
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        if (corpo is not null) pedido.Content = JsonContent.Create(corpo);
        return await cliente.SendAsync(pedido);
    }

    private static async Task ProblemaAsync(HttpResponseMessage resposta, int status, string codigo)
    {
        Assert.AreEqual(status, (int)resposta.StatusCode);
        Assert.AreEqual("application/problem+json", resposta.Content.Headers.ContentType!.MediaType);
        var json = await ApiCliente.JsonAsync(resposta);
        Assert.AreEqual(status, json.GetProperty("status").GetInt32());
        Assert.AreEqual(codigo, json.GetProperty("code").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
    }

    // O discriminador vem por último para verificar que a ordem JSON não é requisito.
    private static object Pf(string nome = "Pessoa sintética", string cpf = "123.000.000-32") =>
        new { nome, cpf, rg = "RG-SINTETICO", cnh = "CNH-SINTETICA", telefone = "000000000", endereco = Endereco(), tipo = "PF" };
    private static object Pj() => new { nome = "PJ sintética", cnpj = "12.ABC.345/01DE-35", telefone = "000000000", endereco = Endereco(), tipo = "PJ" };
    private static object Condutor(Guid clienteId) => new { clienteId, nome = "Condutor sintético", email = "condutor@example.invalid",
        cpf = "123.000.000-32", cnh = "CNH-SINTETICA", validadeCnh = "2028-01-01", telefone = "000000000", proprioCliente = true };
    private static object Endereco() => new { estado = "Estado", cidade = "Cidade", bairro = "Bairro", rua = "Rua", numero = "S/N" };

    private sealed class ColetorLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Mensagens { get; } = new();
        public ILogger CreateLogger(string categoria) => new Coletor(Mensagens);
        public void Dispose() { }
        private sealed class Coletor(ConcurrentQueue<string> mensagens) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? erro, Func<TState, Exception?, string> formatter) =>
                mensagens.Enqueue(formatter(state, erro) + (erro?.ToString() ?? ""));
        }
    }
}
