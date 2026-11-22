using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Locadora.IntegrationTests.Suporte;

public static class ApiCliente
{
    public static async Task<HttpResponseMessage> RegistrarAsync(
        HttpClient cliente,
        string? username,
        string? email,
        string? password,
        Guid? empresaId = null)
    {
        var corpo = new Dictionary<string, object?>
        {
            ["username"] = username,
            ["email"] = email,
            ["password"] = password,
        };
        if (empresaId is not null)
        {
            corpo["empresaId"] = empresaId;
        }

        return await cliente.PostAsJsonAsync("/api/v1/registro", corpo);
    }

    public static Task<HttpResponseMessage> EntrarAsync(HttpClient cliente, string? username, string? password)
        => cliente.PostAsJsonAsync("/api/v1/session/login", new Dictionary<string, string?>
        {
            ["username"] = username,
            ["password"] = password,
        });

    public static Task<HttpResponseMessage> RenovarAsync(HttpClient cliente, string? refresh)
        => cliente.PostAsJsonAsync("/api/v1/session/refresh", new Dictionary<string, string?>
        {
            ["refreshToken"] = refresh,
        });

    public static void Bearer(HttpClient cliente, string token)
    {
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(texto);
        return documento.RootElement.Clone();
    }

    public static bool NoStore(HttpResponseMessage resposta)
    {
        if (resposta.Headers.CacheControl?.NoStore == true)
        {
            return true;
        }

        return resposta.Headers.TryGetValues("Cache-Control", out var valores)
            && valores.Any(valor => valor.Contains("no-store", StringComparison.OrdinalIgnoreCase));
    }

    public static void SemCookie(HttpResponseMessage resposta)
    {
        Assert.IsFalse(resposta.Headers.Contains("Set-Cookie"));
    }

    public static JwtSecurityToken LerToken(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token);

    public static Guid ClaimGuid(JwtSecurityToken token, string tipo)
        => Guid.Parse(token.Claims.Single(claim => claim.Type == tipo).Value);
}

public static class TokenTeste
{
    public static string Emitir(
        Guid contaId,
        Guid empresaId,
        Guid sessaoId,
        DateTimeOffset expiraEm,
        int versao = 1,
        string? kid = "v1",
        string algoritmo = SecurityAlgorithms.HmacSha256,
        string? emissor = null,
        string? audiencia = null,
        byte[]? chave = null)
    {
        var bytes = chave ?? Convert.FromBase64String(AmbienteIntegracao.Chave);
        var chaveAssinatura = new SymmetricSecurityKey(bytes);
        if (kid is not null)
        {
            chaveAssinatura.KeyId = kid;
        }

        var agora = DateTimeOffset.UtcNow;
        var token = new JwtSecurityToken(
            issuer: emissor ?? AmbienteIntegracao.Emissor,
            audience: audiencia ?? AmbienteIntegracao.Audiencia,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, contaId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sid, sessaoId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("versaoAcesso", versao.ToString()),
                new Claim("empresaId", empresaId.ToString()),
            ],
            notBefore: agora.AddMinutes(-1).UtcDateTime,
            expires: expiraEm.UtcDateTime,
            signingCredentials: new SigningCredentials(chaveAssinatura, algoritmo));

        return new JwtSecurityTokenHandler { MapInboundClaims = false }.WriteToken(token);
    }
}
