using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Infrastructure.Seguranca;
using Microsoft.IdentityModel.Tokens;

namespace Locadora.Infrastructure.Modulos.Acesso.Sessao;

public sealed class EmissorJwt
{
    private readonly MapaChavesJwt _mapa;
    private readonly string _emissor;
    private readonly string _audiencia;
    private readonly TimeProvider _tempo;

    public EmissorJwt(MapaChavesJwt mapa, string emissor, string audiencia, TimeProvider tempo)
    {
        _mapa = mapa;
        _emissor = emissor;
        _audiencia = audiencia;
        _tempo = tempo;
    }

    public string Emitir(Guid contaId, Guid empresaId, Guid sessaoId, int versao, DateTimeOffset expiraEm)
    {
        var agora = _tempo.GetUtcNow();
        var chave = new SymmetricSecurityKey(_mapa.ChaveAtiva) { KeyId = _mapa.VersaoAtiva };
        var token = new JwtSecurityToken(
            issuer: _emissor,
            audience: _audiencia,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, contaId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sid, sessaoId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(agora.UtcDateTime).ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
                new Claim("versaoAcesso", versao.ToString(CultureInfo.InvariantCulture)),
                new Claim("empresaId", empresaId.ToString()),
            ],
            notBefore: agora.UtcDateTime,
            expires: expiraEm.UtcDateTime,
            signingCredentials: new SigningCredentials(chave, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler { MapInboundClaims = false }.WriteToken(token);
    }
}

public static class ParametrosToken
{
    public static TokenValidationParameters Criar(MapaChavesJwt mapa, string emissor, string audiencia)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = emissor,
            ValidateAudience = true,
            ValidAudience = audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ClockSkew = PrazosSessao.ToleranciaRelogio,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            TryAllIssuerSigningKeys = false,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            IssuerSigningKeyResolver = (_, _, kid, _) =>
            {
                if (!mapa.TryGet(kid, out var bytes))
                {
                    return [];
                }

                return [new SymmetricSecurityKey(bytes) { KeyId = kid }];
            },
        };
    }
}

public static class GeradorRefresh
{
    public static string Novo()
    {
        return Base64UrlEncoder.Encode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    }

    public static string Hash(string refresh)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(refresh));
        return Convert.ToHexString(bytes);
    }
}
