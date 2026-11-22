using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Locadora.Api.Host;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Api.Modulos.Acesso.Sessao;

public sealed class ValidacaoSessaoMiddleware
{
    private readonly RequestDelegate _proximo;

    public ValidacaoSessaoMiddleware(RequestDelegate proximo)
    {
        _proximo = proximo;
    }

    public async Task InvokeAsync(HttpContext http, LocadoraDbContext banco, ContextoEmpresa contexto, TimeProvider tempo)
    {
        var endpoint = http.GetEndpoint();
        var exigeAutenticacao = endpoint?.Metadata.GetMetadata<IAuthorizeData>() is not null
            && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;
        if (!exigeAutenticacao || http.User.Identity?.IsAuthenticated != true)
        {
            await _proximo(http);
            return;
        }

        try
        {
            if (!Guid.TryParse(http.User.FindFirstValue(JwtRegisteredClaimNames.Sid), out var sessaoId)
                || !Guid.TryParse(http.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var contaId)
                || !Guid.TryParse(http.User.FindFirstValue("empresaId"), out var empresaId)
                || !int.TryParse(http.User.FindFirstValue("versaoAcesso"), out var versao))
            {
                await RecusarAsync(http, StatusCodes.Status401Unauthorized, "Sessão inválida", "sessao_invalida");
                return;
            }

            var sessao = await banco.SessoesAutenticacao.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == sessaoId, http.RequestAborted);
            var agora = tempo.GetUtcNow();
            var logout = http.Request.Path.StartsWithSegments("/api/v1/session/logout");
            if (sessao is null
                || sessao.ContaId != contaId
                || sessao.EmpresaId != empresaId
                || sessao.VersaoAcesso != versao)
            {
                await RecusarAsync(http, StatusCodes.Status401Unauthorized, "Sessão inválida", "sessao_invalida");
                return;
            }

            if (!sessao.Vigente(agora))
            {
                if (logout && sessao.RevogadaEm is not null)
                {
                    contexto.EmpresaId = sessao.EmpresaId;
                    http.Items["sessao-revogada"] = true;
                    await _proximo(http);
                    return;
                }

                await RecusarAsync(http, StatusCodes.Status401Unauthorized, "Sessão inválida", "sessao_invalida");
                return;
            }

            var conta = await banco.Users.IgnoreQueryFilters().FirstOrDefaultAsync(usuario => usuario.Id == contaId, http.RequestAborted);
            if (conta is null || conta.EmpresaId != sessao.EmpresaId)
            {
                await RecusarAsync(http, StatusCodes.Status401Unauthorized, "Sessão inválida", "sessao_invalida");
                return;
            }

            contexto.EmpresaId = sessao.EmpresaId;
            await _proximo(http);
        }
        catch (Exception excecao) when (FalhasArmazenamento.Eh(excecao))
        {
            await RecusarAsync(http, StatusCodes.Status503ServiceUnavailable, "Dependência indisponível", "dependencia_indisponivel");
        }
    }

    private static async Task RecusarAsync(HttpContext http, int status, string title, string code)
    {
        http.Response.StatusCode = status;
        http.Response.ContentType = "application/problem+json";
        http.Response.Headers.CacheControl = "no-store";
        await http.Response.WriteAsync(ProblemaJson.Serializar(status, title, code, http.TraceIdentifier, null));
    }
}
