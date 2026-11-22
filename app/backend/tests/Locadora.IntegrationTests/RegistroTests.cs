using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Application.Acesso.Registro;
using Locadora.Domain.Acesso;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class RegistroTests
{
    [TestMethod]
    public async Task CadastroValidoResponde201SemSessaoNemSegredo()
    {
        using var cliente = Clientes.Criar();
        var senha = "abcdefgh";
        var usuario = "ana" + Guid.NewGuid().ToString("N")[..8];
        var resposta = await ApiCliente.RegistrarAsync(cliente, usuario, usuario + "@exemplo.com", senha);
        var corpo = await resposta.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(corpo).RootElement;

        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        Assert.AreNotEqual(Guid.Empty, json.GetProperty("empresaId").GetGuid());
        Assert.AreNotEqual(Guid.Empty, json.GetProperty("contaId").GetGuid());
        Assert.IsFalse(resposta.Headers.Contains("Location"));
        ApiCliente.SemCookie(resposta);
        Assert.IsFalse(corpo.Contains(senha, StringComparison.Ordinal));
        Assert.IsFalse(corpo.Contains("accessToken", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(corpo.Contains("refreshToken", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(corpo.Contains("hash", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SenhaSemComposicaoEspecialCriaConta()
    {
        using var cliente = Clientes.Criar();
        var usuario = "simples" + Guid.NewGuid().ToString("N")[..8];
        var resposta = await ApiCliente.RegistrarAsync(cliente, usuario, usuario + "@exemplo.com", "!!!!!!!!");
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
    }

    [TestMethod]
    public async Task Validacao400IdentificaCampoENaoEcoaSenha()
    {
        using var cliente = Clientes.Criar();
        var senha = "zxqwvut";
        var antes = await ContarUsuariosAsync();
        var resposta = await ApiCliente.RegistrarAsync(cliente, "ana silva", "sem-arroba", senha);
        var corpo = await resposta.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(corpo).RootElement;

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.AreEqual("validacao", json.GetProperty("code").GetString());
        Assert.IsTrue(json.GetProperty("errors").TryGetProperty("username", out _));
        Assert.IsTrue(json.GetProperty("errors").TryGetProperty("email", out _));
        Assert.IsTrue(json.GetProperty("errors").TryGetProperty("password", out _));
        Assert.IsFalse(corpo.Contains(senha, StringComparison.Ordinal));
        Assert.AreEqual(antes, await ContarUsuariosAsync());
    }

    [TestMethod]
    public async Task SenhaLongaNaoCriaConta()
    {
        using var cliente = Clientes.Criar();
        var usuario = "longa" + Guid.NewGuid().ToString("N")[..8];
        var resposta = await ApiCliente.RegistrarAsync(cliente, usuario, usuario + "@exemplo.com", new string('a', 129));
        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.AreEqual(0, await ContarPorNomeAsync(usuario));
    }

    [TestMethod]
    public async Task AdminEadminConflitamENaoCriamOutraEmpresa()
    {
        using var cliente = Clientes.Criar();
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var primeiroNome = "Admin" + sufixo;
        var segundoNome = "admin" + sufixo;
        var antes = await ContarEmpresasAsync();
        var primeiro = await ApiCliente.RegistrarAsync(cliente, primeiroNome, "admin" + sufixo + "@exemplo.com", "abcdefgh");
        var depoisPrimeiro = await ContarEmpresasAsync();
        var segundo = await ApiCliente.RegistrarAsync(cliente, segundoNome, "outro" + sufixo + "@exemplo.com", "abcdefgh");
        var corpo = await segundo.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(corpo).RootElement;

        Assert.AreEqual(HttpStatusCode.Created, primeiro.StatusCode);
        Assert.AreEqual(antes + 1, depoisPrimeiro);
        Assert.AreEqual(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.AreEqual("usuario_existente", json.GetProperty("code").GetString());
        Assert.IsTrue(json.GetProperty("errors").TryGetProperty("username", out _));
        Assert.AreEqual(depoisPrimeiro, await ContarEmpresasAsync());
        Assert.AreEqual(0, await ContarPorNomeAsync(segundoNome) - await ContarPorNomeAsync(primeiroNome));
        Assert.AreEqual(1, await ContarPorNomeAsync(primeiroNome));
    }

    [TestMethod]
    public async Task EmailRepetidoComUsuarioNovoCriaOutraEmpresa()
    {
        using var cliente = Clientes.Criar();
        var email = "mesmo" + Guid.NewGuid().ToString("N")[..8] + "@exemplo.com";
        var primeiro = await ApiCliente.RegistrarAsync(cliente, "um" + Guid.NewGuid().ToString("N")[..8], email, "abcdefgh");
        var segundo = await ApiCliente.RegistrarAsync(cliente, "dois" + Guid.NewGuid().ToString("N")[..8], email, "abcdefgh");
        var json1 = await ApiCliente.JsonAsync(primeiro);
        var json2 = await ApiCliente.JsonAsync(segundo);

        Assert.AreEqual(HttpStatusCode.Created, primeiro.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, segundo.StatusCode);
        Assert.AreNotEqual(json1.GetProperty("empresaId").GetGuid(), json2.GetProperty("empresaId").GetGuid());
        Assert.AreNotEqual(json1.GetProperty("contaId").GetGuid(), json2.GetProperty("contaId").GetGuid());
    }

    [TestMethod]
    public async Task EmpresaIdDoClienteNaoAnexaContaExistente()
    {
        using var cliente = Clientes.Criar();
        var primeira = await ApiCliente.RegistrarAsync(cliente, "origem" + Guid.NewGuid().ToString("N")[..8], "origem@exemplo.com", "abcdefgh");
        var empresaExistente = (await ApiCliente.JsonAsync(primeira)).GetProperty("empresaId").GetGuid();
        var segunda = await ApiCliente.RegistrarAsync(
            cliente,
            "nova" + Guid.NewGuid().ToString("N")[..8],
            "nova@exemplo.com",
            "abcdefgh",
            empresaExistente);
        var criada = (await ApiCliente.JsonAsync(segunda)).GetProperty("empresaId").GetGuid();

        Assert.AreEqual(HttpStatusCode.Created, segunda.StatusCode);
        Assert.AreNotEqual(empresaExistente, criada);
    }

    [TestMethod]
    public async Task AuditoriaDoSucessoNaoContemSenha()
    {
        using var cliente = Clientes.Criar();
        var senha = "segredo-auditoria-9";
        var usuario = "aud" + Guid.NewGuid().ToString("N")[..8];
        var resposta = await ApiCliente.RegistrarAsync(cliente, usuario, usuario + "@exemplo.com", senha);
        var json = await ApiCliente.JsonAsync(resposta);
        var contaId = json.GetProperty("contaId").GetGuid();
        var empresaId = json.GetProperty("empresaId").GetGuid();

        await using var banco = AmbienteIntegracao.CriarContexto();
        var auditoria = await banco.AuditoriasAcesso.IgnoreQueryFilters().SingleAsync(item => item.ContaId == contaId);
        var texto = JsonSerializer.Serialize(auditoria);

        Assert.AreEqual(empresaId, auditoria.EmpresaId);
        Assert.AreEqual(AuditoriaAcesso.AcaoRegistro, auditoria.Acao);
        Assert.IsTrue(auditoria.Momento > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.IsFalse(texto.Contains(senha, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FalhaNoMeioNaoDeixaEmpresaNemConta()
    {
        var usuario = "falha" + Guid.NewGuid().ToString("N")[..8];
        var senha = "segredo-falha-9";
        var antes = await ContarEmpresasAsync();
        await using var fabrica = new FabricaApi(
            AmbienteIntegracao.Conexao,
            AmbienteIntegracao.Chave,
            servicos: servicos =>
            {
                servicos.RemoveAll<IGanchoRegistro>();
                servicos.AddSingleton<IGanchoRegistro, GanchoQueFalha>();
            });
        using var cliente = Clientes.Criar(fabrica);
        var resposta = await ApiCliente.RegistrarAsync(cliente, usuario, usuario + "@exemplo.com", senha);
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.IsFalse(corpo.Contains(senha, StringComparison.Ordinal));
        Assert.AreEqual(antes, await ContarEmpresasAsync());
        Assert.AreEqual(0, await ContarPorNomeAsync(usuario));
    }

    [TestMethod]
    public async Task SextaRequisicaoRecebe429ComTraceIdENaoCriaConta()
    {
        using var cliente = Clientes.Criar();
        var ip = cliente.DefaultRequestHeaders.GetValues("X-Teste-Ip").Single();
        for (var i = 0; i < 5; i++)
        {
            var invalida = await ApiCliente.RegistrarAsync(cliente, "", "", "");
            Assert.AreEqual(HttpStatusCode.BadRequest, invalida.StatusCode);
        }

        var usuario = "sexta" + Guid.NewGuid().ToString("N")[..8];
        var antes = await ContarEmpresasAsync();
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var sexta = await ApiCliente.RegistrarAsync(cliente, usuario, usuario + "@exemplo.com", "abcdefgh");
        var corpo = await sexta.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(corpo).RootElement;

        Assert.IsTrue(cronometro.Elapsed < TimeSpan.FromSeconds(2));
        Assert.AreEqual(HttpStatusCode.TooManyRequests, sexta.StatusCode);
        Assert.AreEqual("limite_excedido", json.GetProperty("code").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
        Assert.IsFalse(corpo.Contains("existe", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(antes, await ContarEmpresasAsync());
        Assert.AreEqual(0, await ContarPorNomeAsync(usuario));

        using var login = Clientes.Criar(ip: ip);
        var entrada = await ApiCliente.EntrarAsync(login, usuario, "abcdefgh");
        Assert.AreEqual(HttpStatusCode.Unauthorized, entrada.StatusCode);
    }

    [TestMethod]
    public async Task CabecalhoEncaminhadoNaoTrocaOIp()
    {
        var ip = Clientes.ProximoIp();
        using var cliente = Clientes.Criar(ip: ip);
        for (var i = 0; i < 5; i++)
        {
            using var pedido = PedidoRegistro(ip, "8.8.8.8");
            var resposta = await cliente.SendAsync(pedido);
            Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        }

        using var esgotado = PedidoRegistro(ip, "1.1.1.1");
        var sexta = await cliente.SendAsync(esgotado);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, sexta.StatusCode);

        using var outro = Clientes.Criar();
        using var forjado = PedidoRegistro(outro.DefaultRequestHeaders.GetValues("X-Teste-Ip").Single(), "8.8.8.8");
        var liberada = await outro.SendAsync(forjado);
        Assert.AreEqual(HttpStatusCode.BadRequest, liberada.StatusCode);
    }

    [TestMethod]
    [Timeout(20_000)]
    public async Task JanelaSeguinteLiberaOLimite()
    {
        await using var fabrica = new FabricaApi(
            AmbienteIntegracao.Conexao,
            AmbienteIntegracao.Chave,
            extras: new Dictionary<string, string?>
            {
                ["Limites:Registro:Quantidade"] = "5",
                ["Limites:Registro:Janela"] = "00:00:01",
            });
        using var cliente = Clientes.Criar(fabrica);
        for (var i = 0; i < 5; i++)
        {
            var resposta = await ApiCliente.RegistrarAsync(cliente, "", "", "");
            Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        }

        var bloqueada = await ApiCliente.RegistrarAsync(cliente, "", "", "");
        Assert.AreEqual(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(2));
        var liberada = await ApiCliente.RegistrarAsync(cliente, "", "", "");
        Assert.AreEqual(HttpStatusCode.BadRequest, liberada.StatusCode);
    }

    private static HttpRequestMessage PedidoRegistro(string ip, string encaminhado)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/v1/registro")
        {
            Content = JsonContent.Create(new Dictionary<string, string?>
            {
                ["username"] = "",
                ["email"] = "",
                ["password"] = "",
            }),
        };
        pedido.Headers.Add("X-Teste-Ip", ip);
        pedido.Headers.Add("X-Forwarded-For", encaminhado);
        return pedido;
    }

    private static async Task<int> ContarEmpresasAsync()
    {
        await using var banco = AmbienteIntegracao.CriarContexto();
        return await banco.Empresas.CountAsync();
    }

    private static async Task<int> ContarUsuariosAsync()
    {
        await using var banco = AmbienteIntegracao.CriarContexto();
        return await banco.Users.CountAsync();
    }

    private static async Task<int> ContarPorNomeAsync(string usuario)
    {
        var normalizado = NormalizadorUsuario.Normalizar(usuario);
        await using var banco = AmbienteIntegracao.CriarContexto();
        return await banco.Users.CountAsync(conta => conta.NormalizedUserName == normalizado);
    }
}
