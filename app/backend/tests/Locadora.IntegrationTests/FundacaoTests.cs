using System.Diagnostics;
using System.Net;
using System.Text;
using Locadora.Application.Modulos.Acesso;
using Locadora.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Extensions.DependencyInjection;

namespace Locadora.IntegrationTests;

[TestClass]
public sealed class FundacaoTests
{
    [TestMethod]
    public async Task FixtureAplicaSchemaNoCatalogoIsolado()
    {
        await using var banco = AmbienteIntegracao.CriarContexto();
        var tabelas = await ContarTabelaAsync(banco, "Empresas");
        var usuarios = await ContarTabelaAsync(banco, "AspNetUsers");
        var sessoes = await ContarTabelaAsync(banco, "SessoesAutenticacao");
        var refreshes = await ContarTabelaAsync(banco, "RefreshesSessao");
        var auditoria = await ContarTabelaAsync(banco, "AuditoriasAcesso");

        Assert.AreEqual(1, tabelas);
        Assert.AreEqual(1, usuarios);
        Assert.AreEqual(1, sessoes);
        Assert.AreEqual(1, refreshes);
        Assert.AreEqual(1, auditoria);
    }

    [TestMethod]
    public async Task HostForaDeDevelopmentNaoAplicaMigration()
    {
        var conexao = await AmbienteIntegracao.CriarCatalogoVazioAsync();
        try
        {
            await using var fabrica = new FabricaApi(conexao, AmbienteIntegracao.Chave, "Testing");
            using var cliente = fabrica.CreateClient();
            var resposta = await cliente.GetAsync("/health");

            Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
            await using var banco = AmbienteIntegracao.CriarContexto(conexao);
            Assert.AreEqual(0, await ContarTabelaAsync(banco, "Empresas"));
        }
        finally
        {
            await AmbienteIntegracao.RemoverCatalogoAsync(conexao);
        }
    }

    [TestMethod]
    public async Task HostDevelopmentAplicaMigration()
    {
        var conexao = await AmbienteIntegracao.CriarCatalogoVazioAsync();
        try
        {
            await using var fabrica = new FabricaApi(conexao, AmbienteIntegracao.Chave, "Development");
            using var cliente = fabrica.CreateClient();
            var resposta = await cliente.GetAsync("/health");

            Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
            await using var banco = AmbienteIntegracao.CriarContexto(conexao);
            Assert.AreEqual(1, await ContarTabelaAsync(banco, "Empresas"));
        }
        finally
        {
            await AmbienteIntegracao.RemoverCatalogoAsync(conexao);
        }
    }

    [TestMethod]
    public async Task HealthNaoConsultaSql()
    {
        var conexao = "Server=127.0.0.1,1;Database=inexistente;User Id=sa;Password=invalid;TrustServerCertificate=True;Connect Timeout=1";
        await using var fabrica = new FabricaApi(conexao, AmbienteIntegracao.Chave, "Testing");
        using var cliente = fabrica.CreateClient();
        var cronometro = Stopwatch.StartNew();

        for (var i = 0; i < 3; i++)
        {
            var resposta = await cliente.GetAsync("/health");
            var corpo = await resposta.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
            Assert.IsTrue(corpo.Contains("ok", StringComparison.Ordinal));
        }

        Assert.IsTrue(cronometro.Elapsed < TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void LimitesPadraoSaoIndependentes()
    {
        using var escopo = AmbienteIntegracao.Fabrica.Services.CreateScope();
        var limites = escopo.ServiceProvider.GetRequiredService<LimitesRequisicao>();

        Assert.AreEqual(5, limites.RegistroQuantidade);
        Assert.AreEqual(TimeSpan.FromMinutes(10), limites.RegistroJanela);
        Assert.AreEqual(5, limites.LoginQuantidade);
        Assert.AreEqual(TimeSpan.FromMinutes(1), limites.LoginJanela);
    }

    [TestMethod]
    public void ChaveInvalidaNaoIniciaOHost()
    {
        var utf8 = new string('k', 32);
        var curta = Convert.ToBase64String(new byte[16]);

        var semMapa = Capturar(new FabricaApi(AmbienteIntegracao.Conexao, null, incluirChave: false));
        var semConexao = Capturar(new FabricaApi("   ", AmbienteIntegracao.Chave));
        var tamanhoErrado = Capturar(new FabricaApi(AmbienteIntegracao.Conexao, curta));
        var textoUtf8 = Capturar(new FabricaApi(AmbienteIntegracao.Conexao, utf8));

        Assert.IsTrue(MencionaConfiguracao(semMapa));
        Assert.IsTrue(MencionaConfiguracao(semConexao));
        Assert.IsTrue(MencionaConfiguracao(tamanhoErrado));
        Assert.IsTrue(MencionaConfiguracao(textoUtf8));
        Assert.IsFalse(textoUtf8.ToString().Contains(utf8, StringComparison.Ordinal));
        Assert.IsFalse(tamanhoErrado.ToString().Contains(curta, StringComparison.Ordinal));
        Assert.IsFalse(textoUtf8.ToString().Contains(AmbienteIntegracao.Chave, StringComparison.Ordinal));
    }

    [TestMethod]
    public void DecodificarChaveExigeTrintaEDoisBytes()
    {
        var utf8 = new string('k', 32);
        Assert.Throws<ConfiguracaoInvalidaException>(() => ValidadorConfiguracao.DecodificarChave(utf8));
        Assert.Throws<ConfiguracaoInvalidaException>(() =>
            ValidadorConfiguracao.DecodificarChave(Convert.ToBase64String(new byte[16])));

        var bytes = ValidadorConfiguracao.DecodificarChave(AmbienteIntegracao.Chave);
        Assert.AreEqual(32, bytes.Length);
        Assert.IsFalse(Encoding.UTF8.GetBytes(AmbienteIntegracao.Chave).AsSpan().SequenceEqual(bytes));
    }

    [TestMethod]
    public void ArquivosVersionadosNaoTemSegredo()
    {
        var raiz = Raiz();
        var appsettings = File.ReadAllText(Path.Combine(raiz, "app/backend/src/Locadora.Api/appsettings.json"));
        var desenvolvimento = File.ReadAllText(Path.Combine(raiz, "app/backend/src/Locadora.Api/appsettings.Development.json"));
        var compose = File.ReadAllText(Path.Combine(raiz, "compose.yml"));

        foreach (var texto in new[] { appsettings, desenvolvimento })
        {
            Assert.IsFalse(texto.Contains("Password", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(texto.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(texto.Contains("Chaves", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(texto.Contains("Server=", StringComparison.OrdinalIgnoreCase));
        }

        Assert.IsTrue(compose.Contains("mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04", StringComparison.Ordinal));
        Assert.IsTrue(compose.Contains("127.0.0.1:1433:1433", StringComparison.Ordinal));
        Assert.IsTrue(compose.Contains("${MSSQL_SA_PASSWORD}", StringComparison.Ordinal));
        Assert.IsFalse(compose.Contains("-P ", StringComparison.Ordinal));
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(compose, @"MSSQL_SA_PASSWORD:\s*[^$\s{]"));
        if (!string.IsNullOrWhiteSpace(AmbienteIntegracao.Conexao))
        {
            Assert.IsFalse(compose.Contains(AmbienteIntegracao.Conexao, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task PreflightAceitaSomenteAOrigemConfigurada()
    {
        using var cliente = Clientes.Criar();
        var permitida = await PreflightAsync(cliente, "http://localhost:4200");
        var outra = await PreflightAsync(cliente, "http://evil.example");

        Assert.IsTrue(permitida.Headers.TryGetValues("Access-Control-Allow-Origin", out var origem));
        Assert.AreEqual("http://localhost:4200", origem!.Single());
        Assert.IsFalse(permitida.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.IsTrue(permitida.Headers.GetValues("Access-Control-Allow-Methods").Single().Contains("POST", StringComparison.Ordinal));
        Assert.IsFalse(outra.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.IsFalse(outra.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.IsFalse((await outra.Content.ReadAsStringAsync()).Contains('*'));
    }

    private static async Task<HttpResponseMessage> PreflightAsync(HttpClient cliente, string origem)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Options, "/api/v1/session/login");
        pedido.Headers.Add("Origin", origem);
        pedido.Headers.Add("Access-Control-Request-Method", "POST");
        pedido.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return await cliente.SendAsync(pedido);
    }

    private static Exception Capturar(FabricaApi fabrica)
    {
        using (fabrica)
        {
            try
            {
                using var cliente = fabrica.CreateClient();
                cliente.GetAsync("/health").GetAwaiter().GetResult();
                Assert.Fail("O host iniciou com configuração inválida.");
                throw new InvalidOperationException("inesperado");
            }
            catch (Exception excecao) when (excecao is not AssertFailedException)
            {
                return excecao;
            }
        }
    }

    private static bool MencionaConfiguracao(Exception excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is ConfiguracaoInvalidaException)
            {
                return true;
            }
        }

        var texto = excecao.ToString();
        return texto.Contains("ConfiguracaoInvalidaException", StringComparison.Ordinal)
            || texto.Contains("chave JWT", StringComparison.Ordinal)
            || texto.Contains("não está configurada", StringComparison.Ordinal);
    }

    private static async Task<int> ContarTabelaAsync(Locadora.Infrastructure.Persistencia.LocadoraDbContext banco, string tabela)
    {
        var conexao = banco.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync();
        }

        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @nome";
        var parametro = comando.CreateParameter();
        parametro.ParameterName = "@nome";
        parametro.Value = tabela;
        comando.Parameters.Add(parametro);
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }

    private static string Raiz()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "compose.yml")))
            {
                return diretorio.FullName;
            }

            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
