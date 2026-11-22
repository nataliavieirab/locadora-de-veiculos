using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Locadora.IntegrationTests.Modulos.Acesso;

[TestClass]
public sealed class SessaoTests
{
    [TestMethod]
    public async Task LoginValidoEntregaBearerNoCorpoSemCookie()
    {
        var (cliente, usuario, senha) = await ContaNovaAsync();
        using (cliente)
        {
            var resposta = await ApiCliente.EntrarAsync(cliente, usuario, senha);
            var corpo = await resposta.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(corpo).RootElement;
            var token = ApiCliente.LerToken(json.GetProperty("accessToken").GetString()!);
            var expira = new DateTimeOffset(DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc));

            Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
            Assert.IsTrue(ApiCliente.NoStore(resposta));
            ApiCliente.SemCookie(resposta);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json.GetProperty("refreshToken").GetString()));
            Assert.AreEqual("HS256", token.Header.Alg);
            Assert.AreEqual("v1", token.Header.Kid);
            Assert.IsTrue(token.Claims.Any(claim => claim.Type == "empresaId"));
            Assert.IsFalse(token.Claims.Any(claim => claim.Type is "email" or "password" or "documento"));
            Assert.IsFalse(corpo.Contains(senha, StringComparison.Ordinal));
            Assert.IsTrue(expira - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(14));
            Assert.IsTrue(expira - DateTimeOffset.UtcNow < TimeSpan.FromMinutes(16));
        }
    }

    [TestMethod]
    public async Task CredencialInvalidaEGenericaEIgualParaUsuarioInexistente()
    {
        var (cliente, usuario, senha) = await ContaNovaAsync();
        using (cliente)
        {
            var senhaErrada = await ApiCliente.EntrarAsync(cliente, usuario, senha + "-errada");
            using var outro = Clientes.Criar();
            var inexistente = await ApiCliente.EntrarAsync(outro, "ninguem" + Guid.NewGuid().ToString("N")[..8], "qualquer1");
            var corpoErrado = await senhaErrada.Content.ReadAsStringAsync();
            var corpoInexistente = await inexistente.Content.ReadAsStringAsync();
            var jsonErrado = JsonDocument.Parse(corpoErrado).RootElement;
            var jsonInexistente = JsonDocument.Parse(corpoInexistente).RootElement;

            Assert.AreEqual(HttpStatusCode.Unauthorized, senhaErrada.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, inexistente.StatusCode);
            Assert.AreEqual(jsonErrado.GetProperty("code").GetString(), jsonInexistente.GetProperty("code").GetString());
            Assert.AreEqual(jsonErrado.GetProperty("title").GetString(), jsonInexistente.GetProperty("title").GetString());
            Assert.AreEqual("credencial_invalida", jsonErrado.GetProperty("code").GetString());
            Assert.IsFalse(corpoErrado.Contains(senha, StringComparison.Ordinal));
            Assert.IsFalse(corpoErrado.Contains("accessToken", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(corpoInexistente.Contains("não existe", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(corpoInexistente.Contains("inexistente", StringComparison.OrdinalIgnoreCase));
        }
    }

    [TestMethod]
    public async Task QuintaFalhaBloqueiaPorQuinzeMinutos()
    {
        var (cliente, usuario, senha) = await ContaNovaAsync();
        using (cliente)
        {
            for (var i = 0; i < 5; i++)
            {
                var falha = await ApiCliente.EntrarAsync(cliente, usuario, "errada-" + i);
                Assert.AreEqual(HttpStatusCode.Unauthorized, falha.StatusCode);
            }

            using var outroIp = Clientes.Criar();
            var durante = await ApiCliente.EntrarAsync(outroIp, usuario, senha);
            var corpo = await durante.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(corpo).RootElement;

            Assert.AreEqual(HttpStatusCode.Unauthorized, durante.StatusCode);
            Assert.AreEqual("credencial_invalida", json.GetProperty("code").GetString());
            Assert.IsFalse(corpo.Contains("accessToken", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(corpo.Contains("bloque", StringComparison.OrdinalIgnoreCase));

            await using var banco = AmbienteIntegracao.CriarContexto();
            var conta = await banco.Users.SingleAsync(item => item.UserName == usuario);
            Assert.IsNotNull(conta.LockoutEnd);
            Assert.IsTrue(conta.LockoutEnd!.Value > DateTimeOffset.UtcNow.AddMinutes(14));
            Assert.IsTrue(conta.LockoutEnd.Value < DateTimeOffset.UtcNow.AddMinutes(16));
        }
    }

    [TestMethod]
    public async Task SextaTentativaDeLoginRecebe429Proprio()
    {
        using var cliente = Clientes.Criar();
        var ip = cliente.DefaultRequestHeaders.GetValues("X-Teste-Ip").Single();
        for (var i = 0; i < 5; i++)
        {
            var tentativa = await ApiCliente.EntrarAsync(cliente, "ninguem", "errada12");
            Assert.AreEqual(HttpStatusCode.Unauthorized, tentativa.StatusCode);
        }

        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var sexta = await ApiCliente.EntrarAsync(cliente, "ninguem", "errada12");
        var corpo = await sexta.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(corpo).RootElement;

        Assert.IsTrue(cronometro.Elapsed < TimeSpan.FromSeconds(2));
        Assert.AreEqual(HttpStatusCode.TooManyRequests, sexta.StatusCode);
        Assert.AreEqual("limite_excedido", json.GetProperty("code").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
        Assert.IsFalse(corpo.Contains("existe", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(corpo.Contains("accessToken", StringComparison.OrdinalIgnoreCase));

        for (var i = 0; i < 5; i++)
        {
            using var registro = Clientes.Criar(ip: ip);
            var cadastro = await ApiCliente.RegistrarAsync(registro, "", "", "");
            Assert.AreEqual(HttpStatusCode.BadRequest, cadastro.StatusCode);
        }
    }

    [TestMethod]
    public async Task RenovacaoConsomeRefreshEReusoRevogaAFamilia()
    {
        var (cliente, usuario, senha) = await ContaNovaAsync();
        using (cliente)
        {
            var login = await ApiCliente.JsonAsync(await ApiCliente.EntrarAsync(cliente, usuario, senha));
            var access = login.GetProperty("accessToken").GetString()!;
            var refresh = login.GetProperty("refreshToken").GetString()!;
            var sessaoId = ApiCliente.ClaimGuid(ApiCliente.LerToken(access), JwtRegisteredClaimNames.Sid);

            await using (var banco = AmbienteIntegracao.CriarContexto())
            {
                var sessao = await banco.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == sessaoId);
                Assert.IsTrue((sessao.ExpiraEm - sessao.CriadaEm - TimeSpan.FromHours(8)).Duration() < TimeSpan.FromSeconds(1));
            }

            var renovacao = await ApiCliente.RenovarAsync(cliente, refresh);
            Assert.IsTrue(ApiCliente.NoStore(renovacao));
            ApiCliente.SemCookie(renovacao);
            var novo = await ApiCliente.JsonAsync(renovacao);
            var novoAccess = novo.GetProperty("accessToken").GetString()!;
            var novoRefresh = novo.GetProperty("refreshToken").GetString()!;
            Assert.AreNotEqual(access, novoAccess);
            Assert.AreNotEqual(refresh, novoRefresh);

            await using (var banco = AmbienteIntegracao.CriarContexto())
            {
                var sessao = await banco.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == sessaoId);
                Assert.AreEqual(1, await banco.SessoesAutenticacao.IgnoreQueryFilters().CountAsync(item => item.ContaId == sessao.ContaId));
                Assert.IsTrue((sessao.ExpiraEm - sessao.CriadaEm - TimeSpan.FromHours(8)).Duration() < TimeSpan.FromSeconds(1));
                Assert.IsNull(sessao.RevogadaEm);
            }

            ApiCliente.Bearer(cliente, access);
            var antiga = await cliente.GetAsync("/api/v1/session");
            Assert.AreEqual(HttpStatusCode.Unauthorized, antiga.StatusCode);

            ApiCliente.Bearer(cliente, novoAccess);
            var atual = await cliente.GetAsync("/api/v1/session");
            Assert.AreEqual(HttpStatusCode.OK, atual.StatusCode);
            Assert.IsTrue(ApiCliente.NoStore(atual));

            var reuso = await ApiCliente.RenovarAsync(cliente, refresh);
            Assert.AreEqual(HttpStatusCode.Unauthorized, reuso.StatusCode);
            var novoReuso = await ApiCliente.RenovarAsync(cliente, novoRefresh);
            Assert.AreEqual(HttpStatusCode.Unauthorized, novoReuso.StatusCode);
            var depois = await cliente.GetAsync("/api/v1/session");
            Assert.AreEqual(HttpStatusCode.Unauthorized, depois.StatusCode);

            await using var conferido = AmbienteIntegracao.CriarContexto();
            var revogada = await conferido.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == sessaoId);
            Assert.IsNotNull(revogada.RevogadaEm);
        }
    }

    [TestMethod]
    public async Task AcessoPertoDoFimDaSessaoNaoPassaDeQuinzeMinutosNemEstende()
    {
        var (cliente, usuario, senha) = await ContaNovaAsync();
        using (cliente)
        {
            var login = await ApiCliente.JsonAsync(await ApiCliente.EntrarAsync(cliente, usuario, senha));
            var access = login.GetProperty("accessToken").GetString()!;
            var refresh = login.GetProperty("refreshToken").GetString()!;
            var sessaoId = ApiCliente.ClaimGuid(ApiCliente.LerToken(access), JwtRegisteredClaimNames.Sid);
            var fim = DateTimeOffset.UtcNow.AddMinutes(5);

            await using (var banco = AmbienteIntegracao.CriarContexto())
            {
                await banco.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE SessoesAutenticacao SET ExpiraEm = {fim} WHERE Id = {sessaoId}");
            }

            var renovado = await ApiCliente.JsonAsync(await ApiCliente.RenovarAsync(cliente, refresh));
            var token = ApiCliente.LerToken(renovado.GetProperty("accessToken").GetString()!);
            var expira = new DateTimeOffset(DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc));

            Assert.IsTrue((expira - fim).Duration() < TimeSpan.FromSeconds(2));
            Assert.IsTrue(expira < DateTimeOffset.UtcNow.AddMinutes(14));

            await using var bancoFinal = AmbienteIntegracao.CriarContexto();
            var sessao = await bancoFinal.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == sessaoId);
            Assert.IsTrue((sessao.ExpiraEm - fim).Duration() < TimeSpan.FromSeconds(1));
        }
    }

    [TestMethod]
    public async Task TokenForaDaToleranciaERejeitadoEDentroDelaPassa()
    {
        var (cliente, usuario, senha) = await ContaNovaAsync();
        using (cliente)
        {
            var login = await ApiCliente.JsonAsync(await ApiCliente.EntrarAsync(cliente, usuario, senha));
            var access = login.GetProperty("accessToken").GetString()!;
            var jwt = ApiCliente.LerToken(access);
            var contaId = ApiCliente.ClaimGuid(jwt, JwtRegisteredClaimNames.Sub);
            var empresaId = ApiCliente.ClaimGuid(jwt, "empresaId");
            var sessaoId = ApiCliente.ClaimGuid(jwt, JwtRegisteredClaimNames.Sid);

            var expirado = TokenTeste.Emitir(contaId, empresaId, sessaoId, DateTimeOffset.UtcNow.AddSeconds(-31));
            var recente = TokenTeste.Emitir(contaId, empresaId, sessaoId, DateTimeOffset.UtcNow.AddSeconds(-10));
            var algoritmo = TokenTeste.Emitir(
                contaId,
                empresaId,
                sessaoId,
                DateTimeOffset.UtcNow.AddMinutes(5),
                algoritmo: SecurityAlgorithms.HmacSha384,
                chave: System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
            var kid = TokenTeste.Emitir(contaId, empresaId, sessaoId, DateTimeOffset.UtcNow.AddMinutes(5), kid: "v9");
            var semKid = TokenTeste.Emitir(contaId, empresaId, sessaoId, DateTimeOffset.UtcNow.AddMinutes(5), kid: null);
            var emissor = TokenTeste.Emitir(contaId, empresaId, sessaoId, DateTimeOffset.UtcNow.AddMinutes(5), emissor: "outro");
            var audiencia = TokenTeste.Emitir(contaId, empresaId, sessaoId, DateTimeOffset.UtcNow.AddMinutes(5), audiencia: "outra");

            Assert.AreEqual(HttpStatusCode.Unauthorized, await ConsultarAsync(cliente, expirado));
            Assert.AreEqual(HttpStatusCode.Unauthorized, await ConsultarAsync(cliente, algoritmo));
            Assert.AreEqual(HttpStatusCode.Unauthorized, await ConsultarAsync(cliente, kid));
            Assert.AreEqual(HttpStatusCode.Unauthorized, await ConsultarAsync(cliente, semKid));
            Assert.AreEqual(HttpStatusCode.Unauthorized, await ConsultarAsync(cliente, emissor));
            Assert.AreEqual(HttpStatusCode.Unauthorized, await ConsultarAsync(cliente, audiencia));
            Assert.AreEqual(HttpStatusCode.OK, await ConsultarAsync(cliente, recente));
        }
    }

    [TestMethod]
    public async Task LogoutRevogaERepeticaoNaoReabre()
    {
        var (cliente, usuario, senha) = await ContaNovaAsync();
        using (cliente)
        {
            var login = await ApiCliente.JsonAsync(await ApiCliente.EntrarAsync(cliente, usuario, senha));
            var access = login.GetProperty("accessToken").GetString()!;
            var refresh = login.GetProperty("refreshToken").GetString()!;
            var sessaoId = ApiCliente.ClaimGuid(ApiCliente.LerToken(access), JwtRegisteredClaimNames.Sid);
            ApiCliente.Bearer(cliente, access);

            var logout = await cliente.PostAsync("/api/v1/session/logout", new StringContent(""));
            Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);
            ApiCliente.SemCookie(logout);

            var repetido = await cliente.PostAsync("/api/v1/session/logout", new StringContent(""));
            Assert.AreEqual(HttpStatusCode.NoContent, repetido.StatusCode);

            var consulta = await cliente.GetAsync("/api/v1/session");
            var renovacao = await ApiCliente.RenovarAsync(cliente, refresh);
            Assert.AreEqual(HttpStatusCode.Unauthorized, consulta.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, renovacao.StatusCode);

            await using var banco = AmbienteIntegracao.CriarContexto();
            var sessao = await banco.SessoesAutenticacao.IgnoreQueryFilters().SingleAsync(item => item.Id == sessaoId);
            Assert.IsNotNull(sessao.RevogadaEm);
        }
    }

    [TestMethod]
    public async Task SqlIndisponivelRecusaAssinaturaValida()
    {
        var (cliente, usuario, senha) = await ContaNovaAsync();
        using (cliente)
        {
            var login = await ApiCliente.JsonAsync(await ApiCliente.EntrarAsync(cliente, usuario, senha));
            var access = login.GetProperty("accessToken").GetString()!;
            var conexao = "Server=127.0.0.1,1;Database=inexistente;User Id=sa;Password=invalid;TrustServerCertificate=True;Connect Timeout=1";
            await using var fabrica = new FabricaApi(conexao, AmbienteIntegracao.Chave);
            using var ruim = fabrica.CreateClient();
            ruim.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

            var protegida = await ruim.GetAsync("/api/v1/session");
            var entrada = await ApiCliente.EntrarAsync(ruim, usuario, senha);
            var json = await ApiCliente.JsonAsync(protegida);

            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, protegida.StatusCode);
            Assert.AreEqual("dependencia_indisponivel", json.GetProperty("code").GetString());
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, entrada.StatusCode);
        }
    }

    private static async Task<HttpStatusCode> ConsultarAsync(HttpClient cliente, string token)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/v1/session");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resposta = await cliente.SendAsync(pedido);
        return resposta.StatusCode;
    }

    private static async Task<(HttpClient Cliente, string Usuario, string Senha)> ContaNovaAsync()
    {
        var cliente = Clientes.Criar();
        var usuario = "sessao" + Guid.NewGuid().ToString("N")[..10];
        var senha = "abcdefgh";
        var resposta = await ApiCliente.RegistrarAsync(cliente, usuario, usuario + "@exemplo.com", senha);
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        return (cliente, usuario, senha);
    }
}
