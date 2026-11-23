using System.Net;
using Locadora.IntegrationTests.Suporte;

namespace Locadora.IntegrationTests.Modulos.Transversal;

[TestClass]
public sealed class CorsCadastrosTests
{
    [TestMethod]
    [DataRow("PUT")]
    [DataRow("DELETE")]
    public async Task PreflightPermiteMutacoesComIfMatchSemCredenciais(string metodo)
    {
        using var cliente = Clientes.Criar();
        using var pedido = new HttpRequestMessage(HttpMethod.Options, "/api/v1/session");
        pedido.Headers.Add("Origin", "http://localhost:4200");
        pedido.Headers.Add("Access-Control-Request-Method", metodo);
        pedido.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,if-match");

        using var resposta = await cliente.SendAsync(pedido);

        Assert.AreEqual(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.AreEqual("http://localhost:4200", resposta.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.IsTrue(resposta.Headers.GetValues("Access-Control-Allow-Methods").Single().Split(',').Contains(metodo));
        Assert.IsTrue(resposta.Headers.GetValues("Access-Control-Allow-Headers").Single().Split(',')
            .Contains("If-Match", StringComparer.OrdinalIgnoreCase));
        Assert.IsFalse(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
        ApiCliente.SemCookie(resposta);
    }

    [TestMethod]
    [DataRow("https://externa.example.invalid")]
    [DataRow("http://localhost:4201")]
    [DataRow("https://localhost:4200")]
    public async Task PreflightNaoAutorizaOrigemDiferente(string origem)
    {
        using var cliente = Clientes.Criar();
        using var pedido = new HttpRequestMessage(HttpMethod.Options, "/api/v1/session");
        pedido.Headers.Add("Origin", origem);
        pedido.Headers.Add("Access-Control-Request-Method", "PUT");
        pedido.Headers.Add("Access-Control-Request-Headers", "authorization,if-match");

        using var resposta = await cliente.SendAsync(pedido);

        Assert.IsFalse(resposta.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.IsFalse(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [TestMethod]
    public async Task RespostaExponibilizaEtagAoNavegadorSemCuringa()
    {
        using var cliente = Clientes.Criar();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/health");
        pedido.Headers.Add("Origin", "http://localhost:4200");

        using var resposta = await cliente.SendAsync(pedido);

        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        Assert.AreEqual("ETag", resposta.Headers.GetValues("Access-Control-Expose-Headers").Single());
        Assert.IsFalse(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [TestMethod]
    public async Task CabecalhoNaoAprovadoNaoRecebePermissaoDePreflight()
    {
        using var cliente = Clientes.Criar();
        using var pedido = new HttpRequestMessage(HttpMethod.Options, "/api/v1/session");
        pedido.Headers.Add("Origin", "http://localhost:4200");
        pedido.Headers.Add("Access-Control-Request-Method", "DELETE");
        pedido.Headers.Add("Access-Control-Request-Headers", "authorization,if-match,x-empresa-id");

        using var resposta = await cliente.SendAsync(pedido);

        var permitidos = resposta.Headers.TryGetValues("Access-Control-Allow-Headers", out var valores)
            ? string.Join(',', valores).Split(',')
            : [];
        Assert.IsFalse(permitidos.Contains("x-empresa-id", StringComparer.OrdinalIgnoreCase));
        Assert.IsFalse(permitidos.Contains("*"));
    }
}
