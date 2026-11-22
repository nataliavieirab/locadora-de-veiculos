using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Locadora.IntegrationTests.Suporte;

public sealed class FabricaApi : WebApplicationFactory<Program>
{
    private readonly string _conexao;
    private readonly string? _chave;
    private readonly string _ambiente;
    private readonly bool _incluirChave;
    private readonly Dictionary<string, string?> _extras;
    private readonly Action<IServiceCollection>? _servicos;
    private readonly Action<IWebHostBuilder>? _configurar;

    public FabricaApi(
        string conexao,
        string? chave,
        string ambiente = "Testing",
        bool incluirChave = true,
        Dictionary<string, string?>? extras = null,
        Action<IServiceCollection>? servicos = null,
        Action<IWebHostBuilder>? configurar = null)
    {
        _conexao = conexao;
        _chave = chave;
        _ambiente = ambiente;
        _incluirChave = incluirChave;
        _extras = extras ?? [];
        _servicos = servicos;
        _configurar = configurar;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_ambiente);

        // O host mínimo lê a configuração antes de Build. Variáveis de ambiente
        // entram nessa leitura; ConfigureAppConfiguration só entra depois.
        PublicarNoAmbiente();

        builder.ConfigureAppConfiguration((_, configuracao) =>
        {
            configuracao.AddInMemoryCollection(Dados());
        });

        if (_servicos is not null)
        {
            builder.ConfigureTestServices(_servicos);
        }

        _configurar?.Invoke(builder);
    }

    private void PublicarNoAmbiente()
    {
        var atuais = Dados();
        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ParaAmbiente("ConnectionStrings:Locadora"),
            ParaAmbiente("Autenticacao:Jwt:Emissor"),
            ParaAmbiente("Autenticacao:Jwt:Audiencia"),
            ParaAmbiente("Autenticacao:Jwt:VersaoAtiva"),
            ParaAmbiente("Autenticacao:Jwt:Chaves:v1"),
            ParaAmbiente("Cors:Origem"),
            ParaAmbiente("Fuso"),
            ParaAmbiente("Moeda"),
            ParaAmbiente("Limites:Registro:Quantidade"),
            ParaAmbiente("Limites:Registro:Janela"),
            ParaAmbiente("Limites:Login:Quantidade"),
            ParaAmbiente("Limites:Login:Janela"),
        };
        foreach (var chave in atuais.Keys)
        {
            nomes.Add(ParaAmbiente(chave));
        }

        foreach (var nome in nomes)
        {
            Environment.SetEnvironmentVariable(nome, null);
        }

        foreach (var item in atuais)
        {
            if (item.Value is not null)
            {
                Environment.SetEnvironmentVariable(ParaAmbiente(item.Key), item.Value);
            }
        }
    }

    private Dictionary<string, string?> Dados()
    {
        var dados = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Locadora"] = _conexao,
            ["Autenticacao:Jwt:Emissor"] = AmbienteIntegracao.Emissor,
            ["Autenticacao:Jwt:Audiencia"] = AmbienteIntegracao.Audiencia,
            ["Autenticacao:Jwt:VersaoAtiva"] = "v1",
            ["Cors:Origem"] = "http://localhost:4200",
            ["Fuso"] = "America/Sao_Paulo",
            ["Moeda"] = "BRL",
        };
        if (_incluirChave)
        {
            dados["Autenticacao:Jwt:Chaves:v1"] = _chave;
        }

        foreach (var extra in _extras)
        {
            dados[extra.Key] = extra.Value;
        }

        return dados;
    }

    private static string ParaAmbiente(string chave) => chave.Replace(":", "__", StringComparison.Ordinal);
}
