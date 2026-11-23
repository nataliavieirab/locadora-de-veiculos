using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.IntegrationTests.Suporte;

namespace Locadora.IntegrationTests.Modulos.FrotaPrecos;

[TestClass]
public sealed class PrecosServicosHttpTests
{
    private const string Taxas = "/api/v1/taxas-servicos";
    private const string Seguros = "/api/v1/configuracoes/seguros";
    private const string Combustiveis = "/api/v1/configuracoes/combustiveis";

    [TestMethod]
    public async Task TaxaTemLocationEtagPaginacaoEdicaoEExclusaoLogica()
    {
        using var cliente = await AutenticadoAsync();
        using var criar = await cliente.PostAsJsonAsync(Taxas, new { nome = "Assento sintético", preco = "18.123456", modalidade = "Diaria" });
        Assert.AreEqual(HttpStatusCode.Created, criar.StatusCode);
        var json = await ApiCliente.JsonAsync(criar);
        var url = $"{Taxas}/{json.GetProperty("id").GetGuid()}";
        Assert.AreEqual(url, criar.Headers.Location!.ToString());
        Assert.AreEqual("18.123456", json.GetProperty("preco").GetString());
        Assert.AreEqual("Diaria", json.GetProperty("modalidade").GetString());
        using var fixa = await cliente.PostAsJsonAsync(Taxas, new { nome = "Limpeza sintética", preco = "0", modalidade = "Fixa" });
        Assert.AreEqual(HttpStatusCode.Created, fixa.StatusCode);
        using var consulta = await cliente.GetAsync(url);
        Assert.AreEqual(criar.Headers.ETag!.ToString(), consulta.Headers.ETag!.ToString());
        Assert.AreEqual("no-store", consulta.Headers.CacheControl!.ToString());
        using var pagina = await cliente.GetAsync(Taxas + "?page=2&pageSize=1");
        var lista = await ApiCliente.JsonAsync(pagina);
        Assert.AreEqual(2, lista.GetProperty("total").GetInt32());
        Assert.AreEqual(1, lista.GetProperty("items").GetArrayLength());
        var dados = new { nome = "Assento sintético", preco = "20", modalidade = "Diaria" };
        using var semVersao = await cliente.PutAsJsonAsync(url, dados);
        Assert.AreEqual(428, (int)semVersao.StatusCode);
        using var editar = await MutarAsync(cliente, HttpMethod.Put, url, criar.Headers.ETag.ToString(), dados);
        Assert.AreEqual(HttpStatusCode.OK, editar.StatusCode);
        Assert.AreEqual("20.00", (await ApiCliente.JsonAsync(editar)).GetProperty("preco").GetString());
        using var obsoleta = await MutarAsync(cliente, HttpMethod.Delete, url, criar.Headers.ETag.ToString());
        Assert.AreEqual(HttpStatusCode.PreconditionFailed, obsoleta.StatusCode);
        using var excluir = await MutarAsync(cliente, HttpMethod.Delete, url, editar.Headers.ETag!.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, excluir.StatusCode);
        using var ausente = await cliente.GetAsync(url);
        Assert.AreEqual(HttpStatusCode.NotFound, ausente.StatusCode);
    }

    [TestMethod]
    public async Task SegurosDistinguemAusenciaDeZeroESaoIndependentesComEtag()
    {
        using var cliente = await AutenticadoAsync();
        foreach (var tipo in new[] { "Cliente", "Terceiros" })
        {
            using var inicial = await cliente.GetAsync($"{Seguros}/{tipo}");
            var json = await ApiCliente.JsonAsync(inicial);
            Assert.IsFalse(json.GetProperty("configurado").GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, json.GetProperty("precoDiario").ValueKind);
            Assert.IsNull(inicial.Headers.ETag);
        }
        using var criar = await cliente.PutAsJsonAsync($"{Seguros}/Cliente", new { precoDiario = "0" });
        Assert.AreEqual(HttpStatusCode.Created, criar.StatusCode);
        Assert.AreEqual($"{Seguros}/Cliente", criar.Headers.Location!.ToString());
        Assert.AreEqual("0.00", (await ApiCliente.JsonAsync(criar)).GetProperty("precoDiario").GetString());
        using var terceiro = await cliente.PutAsJsonAsync($"{Seguros}/Terceiros", new { precoDiario = "7.123456" });
        Assert.AreEqual(HttpStatusCode.Created, terceiro.StatusCode);
        using var semVersao = await cliente.PutAsJsonAsync($"{Seguros}/Cliente", new { precoDiario = "1" });
        Assert.AreEqual(428, (int)semVersao.StatusCode);
        using var editar = await MutarAsync(cliente, HttpMethod.Put, $"{Seguros}/Cliente", criar.Headers.ETag!.ToString(), new { precoDiario = "5" });
        Assert.AreEqual(HttpStatusCode.OK, editar.StatusCode);
        Assert.AreNotEqual(criar.Headers.ETag.ToString(), editar.Headers.ETag!.ToString());
        using var obsoleta = await MutarAsync(cliente, HttpMethod.Put, $"{Seguros}/Cliente", criar.Headers.ETag.ToString(), new { precoDiario = "6" });
        Assert.AreEqual(HttpStatusCode.PreconditionFailed, obsoleta.StatusCode);
        using var consulta = await cliente.GetAsync($"{Seguros}/Terceiros");
        Assert.AreEqual("7.123456", (await ApiCliente.JsonAsync(consulta)).GetProperty("precoDiario").GetString());
        Assert.AreEqual(terceiro.Headers.ETag!.ToString(), consulta.Headers.ETag!.ToString());
        Assert.AreEqual("no-store", consulta.Headers.CacheControl!.ToString());
    }

    [TestMethod]
    public async Task CombustiveisCriamVersoesComInstanteDoServidorSemEtagMutavel()
    {
        using var cliente = await AutenticadoAsync();
        foreach (var tipo in new[] { "Gasolina", "Gas", "Diesel", "Alcool" })
        {
            using var vazio = await cliente.GetAsync($"{Combustiveis}/{tipo}");
            var inicial = await ApiCliente.JsonAsync(vazio);
            Assert.IsFalse(inicial.GetProperty("configurado").GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, inicial.GetProperty("atual").ValueKind);
            Assert.AreEqual(0, inicial.GetProperty("historico").GetArrayLength());
            var antes = DateTimeOffset.UtcNow;
            using var primeira = await cliente.PutAsJsonAsync($"{Combustiveis}/{tipo}", new { precoUnitario = "0" });
            Assert.AreEqual(HttpStatusCode.Created, primeira.StatusCode);
            var versao = await ApiCliente.JsonAsync(primeira);
            var inicio = versao.GetProperty("inicioVigenciaUtc").GetDateTimeOffset();
            Assert.IsTrue(inicio >= antes && inicio <= DateTimeOffset.UtcNow);
            Assert.IsNull(primeira.Headers.ETag);
            Assert.AreEqual($"{Combustiveis}/{tipo}", primeira.Headers.Location!.ToString());
            using var segunda = await cliente.PutAsJsonAsync($"{Combustiveis}/{tipo}", new { precoUnitario = "6.123456" });
            Assert.AreEqual(HttpStatusCode.Created, segunda.StatusCode);
            var nova = await ApiCliente.JsonAsync(segunda);
            Assert.AreNotEqual(versao.GetProperty("id").GetGuid(), nova.GetProperty("id").GetGuid());
            using var consulta = await cliente.GetAsync($"{Combustiveis}/{tipo}");
            var json = await ApiCliente.JsonAsync(consulta);
            Assert.IsTrue(json.GetProperty("configurado").GetBoolean());
            Assert.AreEqual("6.123456", json.GetProperty("atual").GetProperty("precoUnitario").GetString());
            Assert.AreEqual(2, json.GetProperty("historico").GetArrayLength());
            Assert.AreEqual("0.00", json.GetProperty("historico")[1].GetProperty("precoUnitario").GetString());
            Assert.AreEqual(inicio, json.GetProperty("historico")[1].GetProperty("inicioVigenciaUtc").GetDateTimeOffset());
            Assert.AreEqual("no-store", consulta.Headers.CacheControl!.ToString());
            using var apagar = await cliente.DeleteAsync($"{Combustiveis}/{tipo}");
            Assert.AreEqual(HttpStatusCode.MethodNotAllowed, apagar.StatusCode);
        }
    }

    [TestMethod]
    public async Task ValidacaoRejeitaDecimaisNumericosNegativosCamposExtrasETiposInvalidos()
    {
        using var cliente = await AutenticadoAsync();
        foreach (var (url, campo) in new[] { ($"{Seguros}/Cliente", "precoDiario"), ($"{Combustiveis}/Gasolina", "precoUnitario") })
        {
            foreach (var valor in new object?[] { -1, 1, "-1", "1,25", "1e2", "0.0000001", "10000000000000", null, "" })
            {
                using var resposta = await cliente.PutAsJsonAsync(url, new Dictionary<string, object?> { [campo] = valor });
                Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode, $"{url} {valor}");
                Assert.IsTrue((await ApiCliente.JsonAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
            }
            foreach (var extra in new[] { "empresaId", "inicioVigenciaUtc", "id" })
            {
                using var resposta = await cliente.PutAsJsonAsync(url, new Dictionary<string, object?> { [campo] = "1", [extra] = "externo" });
                Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
            }
        }
        foreach (var (campo, valor) in new (string, object?)[]
        {
            ("nome", ""), ("nome", new string('A', 201)), ("preco", -1), ("preco", "-1"),
            ("preco", "0.0000001"), ("preco", "1,25"), ("modalidade", "Outra"), ("empresaId", Guid.NewGuid()),
        })
        {
            var dados = new Dictionary<string, object?> { ["nome"] = "Taxa", ["preco"] = "1", ["modalidade"] = "Fixa" };
            dados[campo] = valor;
            using var resposta = await cliente.PostAsJsonAsync(Taxas, dados);
            Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        }
        foreach (var url in new[] { $"{Seguros}/Outro", $"{Seguros}/0", $"{Combustiveis}/Eletrico", $"{Combustiveis}/0", Taxas + "?page=0", Taxas + "?pageSize=101" })
        {
            using var resposta = await cliente.GetAsync(url);
            Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        }
    }

    [TestMethod]
    public async Task AutorizacaoIsolaTaxasSegurosECombustiveis()
    {
        using var a = await AutenticadoAsync();
        using var b = await AutenticadoAsync();
        using var semSessao = Clientes.Criar();
        foreach (var url in new[] { Taxas, $"{Seguros}/Cliente", $"{Combustiveis}/Gasolina" })
        {
            using var negada = await semSessao.GetAsync(url);
            Assert.AreEqual(HttpStatusCode.Unauthorized, negada.StatusCode);
        }
        using var taxa = await b.PostAsJsonAsync(Taxas, new { nome = "Taxa B", preco = "9", modalidade = "Fixa" });
        var urlTaxa = taxa.Headers.Location!.ToString();
        using var consulta = await a.GetAsync(urlTaxa);
        using var editar = await MutarAsync(a, HttpMethod.Put, urlTaxa, taxa.Headers.ETag!.ToString(), new { nome = "Outro", preco = "1", modalidade = "Diaria" });
        using var excluir = await MutarAsync(a, HttpMethod.Delete, urlTaxa, taxa.Headers.ETag.ToString());
        foreach (var resposta in new[] { consulta, editar, excluir }) Assert.AreEqual(HttpStatusCode.NotFound, resposta.StatusCode);
        using var seguroB = await b.PutAsJsonAsync($"{Seguros}/Cliente", new { precoDiario = "9" });
        using var combustivelB = await b.PutAsJsonAsync($"{Combustiveis}/Gasolina", new { precoUnitario = "9" });
        using var seguroA = await a.GetAsync($"{Seguros}/Cliente");
        using var combustivelA = await a.GetAsync($"{Combustiveis}/Gasolina");
        Assert.IsFalse((await ApiCliente.JsonAsync(seguroA)).GetProperty("configurado").GetBoolean());
        Assert.IsFalse((await ApiCliente.JsonAsync(combustivelA)).GetProperty("configurado").GetBoolean());
        using var versaoExterna = await MutarAsync(a, HttpMethod.Put, $"{Seguros}/Cliente", seguroB.Headers.ETag!.ToString(), new { precoDiario = "1" });
        Assert.AreEqual(HttpStatusCode.PreconditionFailed, versaoExterna.StatusCode);
        using var novoSeguroA = await a.PutAsJsonAsync($"{Seguros}/Cliente", new { precoDiario = "1" });
        using var novoCombustivelA = await a.PutAsJsonAsync($"{Combustiveis}/Gasolina", new { precoUnitario = "1" });
        Assert.AreEqual(HttpStatusCode.Created, novoSeguroA.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, novoCombustivelA.StatusCode);
        using var atualB = await b.GetAsync($"{Seguros}/Cliente");
        using var combustivelAtualB = await b.GetAsync($"{Combustiveis}/Gasolina");
        Assert.AreEqual("9.00", (await ApiCliente.JsonAsync(atualB)).GetProperty("precoDiario").GetString());
        Assert.AreEqual("9.00", (await ApiCliente.JsonAsync(combustivelAtualB)).GetProperty("atual").GetProperty("precoUnitario").GetString());
    }

    private static async Task<HttpClient> AutenticadoAsync()
    {
        var cliente = Clientes.Criar();
        var usuario = "precos_" + Guid.NewGuid().ToString("N");
        using var registro = await ApiCliente.RegistrarAsync(cliente, usuario, "teste@example.invalid", "Senha123");
        Assert.AreEqual(HttpStatusCode.Created, registro.StatusCode);
        using var login = await ApiCliente.EntrarAsync(cliente, usuario, "Senha123");
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        ApiCliente.Bearer(cliente, (await ApiCliente.JsonAsync(login)).GetProperty("accessToken").GetString()!);
        return cliente;
    }

    private static async Task<HttpResponseMessage> MutarAsync(HttpClient cliente, HttpMethod metodo, string url,
        string etag, object? dados = null)
    {
        using var pedido = new HttpRequestMessage(metodo, url);
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        if (dados is not null) pedido.Content = JsonContent.Create(dados);
        return await cliente.SendAsync(pedido);
    }
}
