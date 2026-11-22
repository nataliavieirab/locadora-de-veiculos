using Locadora.Application.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Persistencia;
using Locadora.Infrastructure.Modulos.Transversal;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[assembly: DoNotParallelize]

namespace Locadora.IntegrationTests.Suporte;

[TestClass]
public sealed class AmbienteIntegracao
{
    public const string Emissor = "locadora-teste";
    public const string Audiencia = "locadora-api-teste";
    public static string Conexao { get; private set; } = "";
    public static string Chave { get; private set; } = "";
    public static FabricaApi Fabrica { get; private set; } = null!;
    private static string _nome = "";

    [AssemblyInitialize]
    public static async Task Iniciar(TestContext contexto)
    {
        var baseConexao = Environment.GetEnvironmentVariable("LOCADORA_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConexao))
        {
            Assert.Fail("Defina LOCADORA_SQL_CONNECTION para os testes de integra\u00e7\u00e3o.");
        }

        _nome = "locadora_" + Guid.NewGuid().ToString("N");
        await ExecutarNoMasterAsync(baseConexao, $"CREATE DATABASE [{_nome}]");
        Conexao = TrocarCatalogo(baseConexao, _nome);
        Chave = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await using (var banco = CriarContexto())
        {
            await banco.Database.MigrateAsync();
        }

        Fabrica = new FabricaApi(Conexao, Chave);
    }

    [AssemblyCleanup]
    public static async Task Encerrar()
    {
        if (Fabrica is not null)
        {
            await Fabrica.DisposeAsync();
        }

        var baseConexao = Environment.GetEnvironmentVariable("LOCADORA_SQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(baseConexao) && !string.IsNullOrWhiteSpace(_nome))
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await ExecutarNoMasterAsync(baseConexao, $"ALTER DATABASE [{_nome}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
            await ExecutarNoMasterAsync(baseConexao, $"DROP DATABASE [{_nome}]");
        }
    }

    public static LocadoraDbContext CriarContexto(string? conexao = null, ContextoEmpresa? contexto = null)
    {
        var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>()
            .UseSqlServer(conexao ?? Conexao)
            .Options;
        var ambiente = contexto ?? new ContextoEmpresa();
        var banco = new LocadoraDbContext(opcoes, ambiente);
        return banco;
    }

    public static LocadoraDbContext CriarContextoComGuarda(ContextoEmpresa contexto, string? conexao = null)
    {
        var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>()
            .UseSqlServer(conexao ?? Conexao)
            .AddInterceptors(new GuardaEscritaEmpresa(contexto))
            .Options;
        return new LocadoraDbContext(opcoes, contexto);
    }

    public static async Task<string> CriarCatalogoVazioAsync()
    {
        var baseConexao = Environment.GetEnvironmentVariable("LOCADORA_SQL_CONNECTION")!;
        var nome = "locadora_" + Guid.NewGuid().ToString("N");
        await ExecutarNoMasterAsync(baseConexao, $"CREATE DATABASE [{nome}]");
        return TrocarCatalogo(baseConexao, nome);
    }

    public static async Task RemoverCatalogoAsync(string conexao)
    {
        Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
        var nome = conexao.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(parte => parte.Split('=', 2))
            .First(par => par[0].Trim().Equals("Database", StringComparison.OrdinalIgnoreCase)
                || par[0].Trim().Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))[1]
            .Trim();
        var baseConexao = Environment.GetEnvironmentVariable("LOCADORA_SQL_CONNECTION")!;
        await ExecutarNoMasterAsync(baseConexao, $"ALTER DATABASE [{nome}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
        await ExecutarNoMasterAsync(baseConexao, $"DROP DATABASE [{nome}]");
    }

    private static async Task ExecutarNoMasterAsync(string conexaoBase, string sql)
    {
        var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>()
            .UseSqlServer(TrocarCatalogo(conexaoBase, "master"))
            .Options;
        await using var banco = new LocadoraDbContext(opcoes, new ContextoEmpresa());
        await banco.Database.ExecuteSqlRawAsync(sql);
    }

    private static string TrocarCatalogo(string conexao, string catalogo)
    {
        var partes = conexao.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(parte =>
            {
                var nome = parte.Split('=', 2)[0].Trim();
                return !nome.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    && !nome.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
        partes.Add($"Database={catalogo}");
        return string.Join(';', partes);
    }
}

public sealed class GanchoQueFalha : IGanchoRegistro
{
    public Task DepoisDaContaAsync(CancellationToken cancelamento)
        => throw new InvalidOperationException("falha de teste");
}

public static class Clientes
{
    private static int _sequencia = 20;

    public static string ProximoIp()
    {
        var numero = Interlocked.Increment(ref _sequencia);
        return $"10.8.{(numero >> 8) & 255}.{numero & 255}";
    }

    public static HttpClient Criar(WebApplicationFactory<Program>? fabrica = null, string? ip = null)
    {
        var cliente = (fabrica ?? AmbienteIntegracao.Fabrica).CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Teste-Ip", ip ?? ProximoIp());
        return cliente;
    }
}
