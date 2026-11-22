using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using Locadora.Domain.Transversal;
using Locadora.Infrastructure.Transversal;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class IsolamentoTests
{
    [TestMethod]
    public async Task ConsultaDeANaoDevolveNemAlteraB()
    {
        var a = await AutenticarAsync("isola-a");
        var b = await AutenticarAsync("isola-b");
        using var clienteA = a.Cliente;
        using var clienteB = b.Cliente;

        using var pedido = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/session?empresaId={b.EmpresaId}");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", a.Access);
        pedido.Headers.Add("X-Empresa-Id", b.EmpresaId.ToString());
        var resposta = await clienteA.SendAsync(pedido);
        var json = await ApiCliente.JsonAsync(resposta);

        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        Assert.AreEqual(a.EmpresaId, json.GetProperty("empresaId").GetGuid());
        Assert.AreEqual(a.ContaId, json.GetProperty("contaId").GetGuid());
        Assert.IsTrue(ApiCliente.NoStore(resposta));

        var consultaB = await ConsultarAsync(clienteB, b.Access);
        var jsonB = await ApiCliente.JsonAsync(consultaB);
        Assert.AreEqual(b.EmpresaId, jsonB.GetProperty("empresaId").GetGuid());
        Assert.AreEqual(b.ContaId, jsonB.GetProperty("contaId").GetGuid());

        ApiCliente.Bearer(clienteA, a.Access);
        var logout = await clienteA.PostAsync("/api/v1/session/logout", new StringContent(""));
        Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);

        var depois = await ConsultarAsync(clienteB, b.Access);
        Assert.AreEqual(HttpStatusCode.OK, depois.StatusCode);
        await using var banco = AmbienteIntegracao.CriarContexto();
        var sessaoB = await banco.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == b.SessaoId);
        Assert.IsNull(sessaoB.RevogadaEm);
        var sessaoA = await banco.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == a.SessaoId);
        Assert.IsNotNull(sessaoA.RevogadaEm);
    }

    [TestMethod]
    public async Task OperacaoProtegidaSemSessaoNaoDevolveDados()
    {
        var a = await AutenticarAsync("sem-sessao");
        using var cliente = Clientes.Criar();
        var consulta = await cliente.GetAsync("/api/v1/session");
        var logout = await cliente.PostAsync("/api/v1/session/logout", new StringContent(""));
        var textoConsulta = await consulta.Content.ReadAsStringAsync();
        var textoLogout = await logout.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.Unauthorized, consulta.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, logout.StatusCode);
        Assert.IsFalse(textoConsulta.Contains(a.EmpresaId.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(textoLogout.Contains(a.EmpresaId.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(textoConsulta.Contains(a.ContaId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task FiltroDeLeituraNaoSubstituiAGuardaDeEscrita()
    {
        var a = await AutenticarAsync("guarda-a");
        var b = await AutenticarAsync("guarda-b");
        using var _ = a.Cliente;
        using var __ = b.Cliente;

        var contextoA = new ContextoEmpresa { EmpresaId = a.EmpresaId };
        await using (var leitura = AmbienteIntegracao.CriarContexto(contexto: contextoA))
        {
            var ids = await leitura.SessoesAutenticacao.Select(item => item.Id).ToListAsync();
            Assert.IsTrue(ids.Contains(a.SessaoId));
            Assert.IsFalse(ids.Contains(b.SessaoId));
        }

        await using (var semGuarda = AmbienteIntegracao.CriarContexto(contexto: contextoA))
        {
            var sessaoB = await semGuarda.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == b.SessaoId);
            sessaoB.Revogar(DateTimeOffset.UtcNow);
            await semGuarda.SaveChangesAsync();
        }

        await using (var conferido = AmbienteIntegracao.CriarContexto())
        {
            var sessaoB = await conferido.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == b.SessaoId);
            Assert.IsNotNull(sessaoB.RevogadaEm);
            await conferido.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE SessoesAutenticacao SET RevogadaEm = NULL WHERE Id = {b.SessaoId}");
        }

        var contextoVazio = new ContextoEmpresa();
        await using var comGuarda = AmbienteIntegracao.CriarContextoComGuarda(contextoA);
        var alheia = await comGuarda.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == b.SessaoId);
        alheia.Revogar(DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<EmpresaDivergenteException>(() => comGuarda.SaveChangesAsync());

        await using var fechada = AmbienteIntegracao.CriarContextoComGuarda(contextoVazio);
        var sessao = await fechada.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == a.SessaoId);
        sessao.Revogar(DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<EmpresaDivergenteException>(() => fechada.SaveChangesAsync());

        await using var banco = AmbienteIntegracao.CriarContexto();
        var intacta = await banco.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == b.SessaoId);
        Assert.IsNull(intacta.RevogadaEm);
    }

    private static async Task<HttpResponseMessage> ConsultarAsync(HttpClient cliente, string token)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/v1/session");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await cliente.SendAsync(pedido);
    }

    private static async Task<ContaAutenticada> AutenticarAsync(string prefixo)
    {
        var cliente = Clientes.Criar();
        var usuario = prefixo + Guid.NewGuid().ToString("N")[..8];
        var registro = await ApiCliente.RegistrarAsync(cliente, usuario, usuario + "@exemplo.com", "abcdefgh");
        Assert.AreEqual(HttpStatusCode.Created, registro.StatusCode);
        var login = await ApiCliente.JsonAsync(await ApiCliente.EntrarAsync(cliente, usuario, "abcdefgh"));
        var access = login.GetProperty("accessToken").GetString()!;
        var jwt = ApiCliente.LerToken(access);
        return new ContaAutenticada(
            cliente,
            access,
            ApiCliente.ClaimGuid(jwt, JwtRegisteredClaimNames.Sub),
            ApiCliente.ClaimGuid(jwt, "empresaId"),
            ApiCliente.ClaimGuid(jwt, JwtRegisteredClaimNames.Sid));
    }

    private sealed record ContaAutenticada(HttpClient Cliente, string Access, Guid ContaId, Guid EmpresaId, Guid SessaoId);
}
