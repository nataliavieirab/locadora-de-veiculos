using System.Net;
using Locadora.IntegrationTests.Suporte;

namespace Locadora.IntegrationTests.Modulos.Transversal;

[TestClass]
public sealed class SuperficieFase2Tests
{
    [TestMethod]
    public async Task OpenApiContemCadastrosSemRotasDeFotoOuAluguelEEntradaSemEmpresa()
    {
        using var cliente = Clientes.Criar();
        using var resposta = await cliente.GetAsync("/openapi/v1.json");
        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        var json = await ApiCliente.JsonAsync(resposta);
        var caminhos = json.GetProperty("paths");
        foreach (var rota in new[]
        {
            "/api/v1/grupos", "/api/v1/planos", "/api/v1/veiculos", "/api/v1/clientes", "/api/v1/condutores",
            "/api/v1/taxas-servicos", "/api/v1/configuracoes/seguros/{tipo}", "/api/v1/configuracoes/combustiveis/{tipo}",
        })
        {
            Assert.IsTrue(caminhos.TryGetProperty(rota, out _), rota);
        }
        Assert.IsFalse(caminhos.EnumerateObject().Any(item =>
            item.Name.Contains("foto", StringComparison.OrdinalIgnoreCase)
            || item.Name.Contains("alugue", StringComparison.OrdinalIgnoreCase)
            || item.Name.Contains("__test", StringComparison.OrdinalIgnoreCase)));
        var esquemas = json.GetProperty("components").GetProperty("schemas");
        foreach (var esquema in esquemas.EnumerateObject().Where(item => item.Name.StartsWith("Entrada", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(esquema.Value.GetRawText(), "empresaId");
        }
        var texto = json.GetRawText();
        Assert.DoesNotContain(texto, AmbienteIntegracao.Chave);
        Assert.DoesNotContain(texto, AmbienteIntegracao.Conexao);
    }
}
