using System.Text;
using Locadora.Application.Modulos.Acesso;
using Microsoft.Extensions.Configuration;

namespace Locadora.Infrastructure.Seguranca;

public sealed class ConfiguracaoInvalidaException : Exception
{
    public ConfiguracaoInvalidaException(string mensagem)
        : base(mensagem)
    {
    }
}

public sealed class MapaChavesJwt
{
    private readonly IReadOnlyDictionary<string, byte[]> _chaves;

    public MapaChavesJwt(string versaoAtiva, IReadOnlyDictionary<string, byte[]> chaves)
    {
        VersaoAtiva = versaoAtiva;
        _chaves = chaves;
    }

    public string VersaoAtiva { get; }

    public byte[] ChaveAtiva => _chaves[VersaoAtiva];

    public bool TryGet(string? kid, out byte[] bytes)
    {
        if (string.IsNullOrEmpty(kid))
        {
            bytes = [];
            return false;
        }

        return _chaves.TryGetValue(kid, out bytes!);
    }
}

public sealed class ConfiguracaoAplicacao
{
    public ConfiguracaoAplicacao(
        string conexao,
        string emissor,
        string audiencia,
        string origem,
        MapaChavesJwt chaves,
        LimitesRequisicao limites,
        string fuso,
        string moeda)
    {
        Conexao = conexao;
        Emissor = emissor;
        Audiencia = audiencia;
        Origem = origem;
        Chaves = chaves;
        Limites = limites;
        Fuso = fuso;
        Moeda = moeda;
    }

    public string Conexao { get; }
    public string Emissor { get; }
    public string Audiencia { get; }
    public string Origem { get; }
    public MapaChavesJwt Chaves { get; }
    public LimitesRequisicao Limites { get; }
    public string Fuso { get; }
    public string Moeda { get; }
}

public static class ValidadorConfiguracao
{
    public static ConfiguracaoAplicacao Carregar(IConfiguration configuration)
    {
        var conexao = configuration.GetConnectionString("Locadora");
        if (string.IsNullOrWhiteSpace(conexao))
        {
            throw new ConfiguracaoInvalidaException("A conexão do banco não está configurada.");
        }

        var emissor = Exigir(configuration["Autenticacao:Jwt:Emissor"], "O emissor do JWT não está configurado.");
        var audiencia = Exigir(configuration["Autenticacao:Jwt:Audiencia"], "A audiência do JWT não está configurada.");
        var versao = Exigir(configuration["Autenticacao:Jwt:VersaoAtiva"], "A versão ativa do JWT não está configurada.");
        var origem = Exigir(configuration["Cors:Origem"], "A origem da interface não está configurada.");
        if (!OrigemExata(origem))
        {
            throw new ConfiguracaoInvalidaException("A origem da interface precisa ser exata.");
        }

        var filhos = configuration.GetSection("Autenticacao:Jwt:Chaves").GetChildren().ToList();
        if (filhos.Count == 0)
        {
            throw new ConfiguracaoInvalidaException("O mapa de chaves JWT não está configurado.");
        }

        var chaves = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var filho in filhos)
        {
            if (string.IsNullOrWhiteSpace(filho.Key)
                || filho.Key.Equals("default", StringComparison.OrdinalIgnoreCase)
                || filho.Key.Equals("padrao", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfiguracaoInvalidaException("O mapa de chaves JWT contém um identificador inválido.");
            }

            chaves[filho.Key] = DecodificarChave(filho.Value);
        }

        if (!chaves.ContainsKey(versao))
        {
            throw new ConfiguracaoInvalidaException("A versão ativa do JWT não existe no mapa.");
        }

        return new ConfiguracaoAplicacao(
            conexao,
            emissor,
            audiencia,
            origem,
            new MapaChavesJwt(versao, chaves),
            LerLimites(configuration),
            configuration["Fuso"] ?? "America/Sao_Paulo",
            configuration["Moeda"] ?? "BRL");
    }

    public static byte[] DecodificarChave(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new ConfiguracaoInvalidaException("A chave JWT precisa ser Base64 de 32 bytes.");
        }

        if (Encoding.UTF8.GetByteCount(texto) == 32)
        {
            throw new ConfiguracaoInvalidaException("A chave JWT não pode ser o texto UTF-8 de 32 bytes.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(texto);
        }
        catch (FormatException)
        {
            throw new ConfiguracaoInvalidaException("A chave JWT precisa ser Base64 de 32 bytes.");
        }

        if (bytes.Length != 32)
        {
            throw new ConfiguracaoInvalidaException("A chave JWT precisa ser Base64 de 32 bytes.");
        }

        var utf8 = Encoding.UTF8.GetBytes(texto);
        if (utf8.Length == bytes.Length && utf8.AsSpan().SequenceEqual(bytes))
        {
            throw new ConfiguracaoInvalidaException("A chave JWT não pode ser usada como texto UTF-8.");
        }

        return bytes;
    }

    private static LimitesRequisicao LerLimites(IConfiguration configuration)
    {
        var registroQuantidade = configuration.GetValue("Limites:Registro:Quantidade", 5);
        var registroJanela = configuration.GetValue("Limites:Registro:Janela", TimeSpan.FromMinutes(10));
        var loginQuantidade = configuration.GetValue("Limites:Login:Quantidade", 5);
        var loginJanela = configuration.GetValue("Limites:Login:Janela", TimeSpan.FromMinutes(1));
        if (registroQuantidade < 1 || loginQuantidade < 1 || registroJanela <= TimeSpan.Zero || loginJanela <= TimeSpan.Zero)
        {
            throw new ConfiguracaoInvalidaException("Os limites de requisição são inválidos.");
        }

        return new LimitesRequisicao(registroQuantidade, registroJanela, loginQuantidade, loginJanela);
    }

    private static string Exigir(string? valor, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ConfiguracaoInvalidaException(mensagem);
        }

        return valor;
    }

    private static bool OrigemExata(string origem)
    {
        if (origem.Contains('*', StringComparison.Ordinal) || origem.Contains(',', StringComparison.Ordinal))
        {
            return false;
        }

        return Uri.TryCreate(origem, UriKind.Absolute, out var uri)
            && string.IsNullOrEmpty(uri.UserInfo)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
