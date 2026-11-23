using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.IntegrationTests.Suporte;

namespace Locadora.IntegrationTests.Modulos.FrotaPrecos;

[TestClass]
public sealed class VeiculosHttpTests
{
    [TestMethod]
    public async Task CadastroSemFotoRetornaLocationEtagDecimaisEPaginacaoComFiltro()
    {
        using var cliente = await AutenticadoAsync();
        var grupo = await GrupoAsync(cliente);
        var outro = await GrupoAsync(cliente);
        var veiculo = await CriarAsync(cliente, Dados(grupo));
        await CriarAsync(cliente, Dados(outro, "XYZ-9A87"));
        Assert.AreEqual("48.125", veiculo.Json.GetProperty("capacidadeLitros").GetString());
        Assert.AreEqual(JsonValueKind.String, veiculo.Json.GetProperty("capacidadeLitros").ValueKind);
        Assert.IsFalse(veiculo.Json.EnumerateObject().Any(item => item.Name.Contains("foto", StringComparison.OrdinalIgnoreCase)));
        using var consulta = await cliente.GetAsync($"/api/v1/veiculos/{veiculo.Id}");
        Assert.AreEqual(veiculo.Etag, consulta.Headers.ETag!.ToString());
        Assert.AreEqual("no-store", consulta.Headers.CacheControl!.ToString());
        using var lista = await cliente.GetAsync($"/api/v1/veiculos?grupoId={grupo}&page=1&pageSize=1");
        var json = await ApiCliente.JsonAsync(lista);
        Assert.AreEqual(1, json.GetProperty("total").GetInt32());
        Assert.AreEqual(1, json.GetProperty("pageSize").GetInt32());
        Assert.AreEqual(veiculo.Id, json.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var vazio = await cliente.GetAsync($"/api/v1/veiculos?grupoId={Guid.NewGuid()}");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(vazio)).GetProperty("total").GetInt32());
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?grupoId=invalido" })
        {
            using var invalida = await cliente.GetAsync("/api/v1/veiculos" + query);
            Assert.AreEqual(HttpStatusCode.BadRequest, invalida.StatusCode);
        }
    }

    [TestMethod]
    public async Task ContratoValidaCamposSemTruncarOuAceitarFoto()
    {
        using var cliente = await AutenticadoAsync();
        var grupo = await GrupoAsync(cliente);
        foreach (var (campo, valor) in new (string, object?)[]
        {
            ("placa", ""), ("placa", new string('A', 17)), ("marca", new string('M', 101)), ("cor", ""),
            ("modelo", ""), ("combustivel", "Eletrico"), ("capacidadeLitros", "0"), ("capacidadeLitros", "-1"),
            ("capacidadeLitros", "1.0001"), ("capacidadeLitros", "1.0000"), ("capacidadeLitros", 48),
            ("capacidadeLitros", "1000000000"), ("capacidadeLitros", "1,25"), ("ano", 0),
            ("grupoId", Guid.Empty), ("foto", "imagem.png"),
        })
        {
            var dados = Dados(grupo);
            dados[campo] = valor;
            using var resposta = await cliente.PostAsJsonAsync("/api/v1/veiculos", dados);
            Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode, campo);
            Assert.AreEqual("application/problem+json", resposta.Content.Headers.ContentType!.MediaType);
        }
        using var lista = await cliente.GetAsync("/api/v1/veiculos");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(lista)).GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task UnicidadeEtagEdicaoExclusaoENovaIdentidade()
    {
        using var cliente = await AutenticadoAsync();
        var grupo = await GrupoAsync(cliente);
        var primeiro = await CriarAsync(cliente, Dados(grupo));
        using var duplicada = await cliente.PostAsJsonAsync("/api/v1/veiculos", Dados(grupo, "abc1d23"));
        Assert.AreEqual(HttpStatusCode.Conflict, duplicada.StatusCode);
        Assert.IsTrue((await ApiCliente.JsonAsync(duplicada)).GetProperty("errors").TryGetProperty("placa", out _));
        var dados = Dados(grupo);
        dados["cor"] = "Prata";
        using var semVersao = await cliente.PutAsJsonAsync($"/api/v1/veiculos/{primeiro.Id}", dados);
        Assert.AreEqual(428, (int)semVersao.StatusCode);
        using var atualizada = await MutarAsync(cliente, HttpMethod.Put, primeiro.Id, primeiro.Etag, dados);
        Assert.AreEqual(HttpStatusCode.OK, atualizada.StatusCode);
        Assert.AreEqual("Prata", (await ApiCliente.JsonAsync(atualizada)).GetProperty("cor").GetString());
        Assert.AreNotEqual(primeiro.Etag, atualizada.Headers.ETag!.ToString());
        using var obsoleta = await MutarAsync(cliente, HttpMethod.Delete, primeiro.Id, primeiro.Etag);
        Assert.AreEqual(412, (int)obsoleta.StatusCode);
        using var excluida = await MutarAsync(cliente, HttpMethod.Delete, primeiro.Id, atualizada.Headers.ETag.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, excluida.StatusCode);
        using var ausente = await cliente.GetAsync($"/api/v1/veiculos/{primeiro.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, ausente.StatusCode);
        var novo = await CriarAsync(cliente, Dados(grupo));
        Assert.AreNotEqual(primeiro.Id, novo.Id);
    }

    [TestMethod]
    public async Task AutorizacaoIsolaLeiturasMutacoesEGrupoDeOutraEmpresa()
    {
        using var a = await AutenticadoAsync();
        using var b = await AutenticadoAsync();
        var grupoA = await GrupoAsync(a);
        var grupoB = await GrupoAsync(b);
        var veiculoB = await CriarAsync(b, Dados(grupoB));
        using var semSessao = Clientes.Criar();
        using var negada = await semSessao.GetAsync("/api/v1/veiculos");
        Assert.AreEqual(HttpStatusCode.Unauthorized, negada.StatusCode);
        using var leitura = await a.GetAsync($"/api/v1/veiculos/{veiculoB.Id}");
        using var alteracao = await MutarAsync(a, HttpMethod.Put, veiculoB.Id, veiculoB.Etag, Dados(grupoA));
        using var exclusao = await MutarAsync(a, HttpMethod.Delete, veiculoB.Id, veiculoB.Etag);
        using var associacao = await a.PostAsJsonAsync("/api/v1/veiculos", Dados(grupoB));
        foreach (var resposta in new[] { leitura, alteracao, exclusao, associacao })
            Assert.AreEqual(HttpStatusCode.NotFound, resposta.StatusCode);
        using var lista = await a.GetAsync($"/api/v1/veiculos?grupoId={grupoB}");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(lista)).GetProperty("total").GetInt32());
        await CriarAsync(a, Dados(grupoA));
    }

    [TestMethod]
    public async Task NaoExpoeRotasDeFotoNaFase2()
    {
        using var cliente = await AutenticadoAsync();
        var veiculo = await CriarAsync(cliente, Dados(await GrupoAsync(cliente)));
        foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Post })
        {
            using var pedido = new HttpRequestMessage(metodo, $"/api/v1/veiculos/{veiculo.Id}/foto");
            using var resposta = await cliente.SendAsync(pedido);
            Assert.AreEqual(HttpStatusCode.NotFound, resposta.StatusCode);
        }
    }

    private static Dictionary<string, object?> Dados(Guid grupo, string placa = "ABC-1D23") => new()
    {
        ["placa"] = placa, ["marca"] = "Marca sintética", ["cor"] = "Azul", ["modelo"] = "Modelo sintético",
        ["combustivel"] = "Gasolina", ["capacidadeLitros"] = "48.125", ["ano"] = 2026, ["grupoId"] = grupo,
    };

    private static async Task<HttpClient> AutenticadoAsync()
    {
        var cliente = Clientes.Criar();
        var username = "veiculos_" + Guid.NewGuid().ToString("N");
        using var registro = await ApiCliente.RegistrarAsync(cliente, username, "teste@example.invalid", "Senha123");
        Assert.AreEqual(HttpStatusCode.Created, registro.StatusCode);
        using var login = await ApiCliente.EntrarAsync(cliente, username, "Senha123");
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        ApiCliente.Bearer(cliente, (await ApiCliente.JsonAsync(login)).GetProperty("accessToken").GetString()!);
        return cliente;
    }

    private static async Task<Guid> GrupoAsync(HttpClient cliente)
    {
        using var resposta = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nome = "Grupo sintético" });
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        return (await ApiCliente.JsonAsync(resposta)).GetProperty("id").GetGuid();
    }

    private static async Task<(Guid Id, string Etag, JsonElement Json)> CriarAsync(HttpClient cliente, object dados)
    {
        using var resposta = await cliente.PostAsJsonAsync("/api/v1/veiculos", dados);
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        var json = await ApiCliente.JsonAsync(resposta);
        var id = json.GetProperty("id").GetGuid();
        Assert.AreEqual($"/api/v1/veiculos/{id}", resposta.Headers.Location!.ToString());
        return (id, resposta.Headers.ETag!.ToString(), json);
    }

    private static async Task<HttpResponseMessage> MutarAsync(HttpClient cliente, HttpMethod metodo, Guid id, string etag, object? dados = null)
    {
        using var pedido = new HttpRequestMessage(metodo, $"/api/v1/veiculos/{id}");
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        if (dados is not null) pedido.Content = JsonContent.Create(dados);
        return await cliente.SendAsync(pedido);
    }
}
