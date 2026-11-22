using System.IdentityModel.Tokens.Jwt;
using Locadora.Application.Acesso.Sessao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace Locadora.Api.Acesso;

public sealed class EntradaLogin
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class EntradaRefresh
{
    public string? RefreshToken { get; set; }
}

[ApiController]
[ServiceFilter(typeof(FiltroSemCache))]
public sealed class SessaoController : ControllerBase
{
    private readonly IServicoSessao _sessao;

    public SessaoController(IServicoSessao sessao)
    {
        _sessao = sessao;
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("/api/v1/session/login")]
    public async Task<IActionResult> Entrar([FromBody] EntradaLogin? entrada, CancellationToken cancelamento)
    {
        var resultado = await _sessao.EntrarAsync(entrada?.Username, entrada?.Password, cancelamento);
        return resultado switch
        {
            ResultadoLogin.Ok ok => Ok(new
            {
                accessToken = ok.AccessToken,
                refreshToken = ok.RefreshToken,
                expiraEm = ok.ExpiraEm,
                contaId = ok.ContaId,
                empresaId = ok.EmpresaId,
            }),
            ResultadoLogin.Indisponivel => Problema(StatusCodes.Status503ServiceUnavailable, "Dependência indisponível", "dependencia_indisponivel"),
            _ => Problema(StatusCodes.Status401Unauthorized, "Credencial inválida", "credencial_invalida"),
        };
    }

    [AllowAnonymous]
    [HttpPost("/api/v1/session/refresh")]
    public async Task<IActionResult> Renovar([FromBody] EntradaRefresh? entrada, CancellationToken cancelamento)
    {
        var resultado = await _sessao.RenovarAsync(entrada?.RefreshToken, cancelamento);
        return resultado switch
        {
            ResultadoRenovacao.Ok ok => Ok(new
            {
                accessToken = ok.AccessToken,
                refreshToken = ok.RefreshToken,
                expiraEm = ok.ExpiraEm,
                contaId = ok.ContaId,
                empresaId = ok.EmpresaId,
            }),
            ResultadoRenovacao.Indisponivel => Problema(StatusCodes.Status503ServiceUnavailable, "Dependência indisponível", "dependencia_indisponivel"),
            _ => Problema(StatusCodes.Status401Unauthorized, "Sessão inválida", "sessao_invalida"),
        };
    }

    [Authorize]
    [HttpGet("/api/v1/session")]
    public async Task<IActionResult> Consultar(CancellationToken cancelamento)
    {
        var sessaoId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sid)!.Value);
        var resultado = await _sessao.ConsultarAsync(sessaoId, cancelamento);
        return resultado switch
        {
            ResultadoConsulta.Ok ok => Ok(new { contaId = ok.ContaId, empresaId = ok.EmpresaId }),
            ResultadoConsulta.Indisponivel => Problema(StatusCodes.Status503ServiceUnavailable, "Dependência indisponível", "dependencia_indisponivel"),
            _ => Problema(StatusCodes.Status401Unauthorized, "Sessão inválida", "sessao_invalida"),
        };
    }

    [Authorize]
    [HttpPost("/api/v1/session/logout")]
    public async Task<IActionResult> Sair(CancellationToken cancelamento)
    {
        if (HttpContext.Items.ContainsKey("sessao-revogada"))
        {
            return NoContent();
        }

        var sessaoId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sid)!.Value);
        var resultado = await _sessao.SairAsync(sessaoId, cancelamento);
        return resultado switch
        {
            ResultadoLogout.Ok => NoContent(),
            ResultadoLogout.Indisponivel => Problema(StatusCodes.Status503ServiceUnavailable, "Dependência indisponível", "dependencia_indisponivel"),
            _ => Problema(StatusCodes.Status401Unauthorized, "Sessão inválida", "sessao_invalida"),
        };
    }

    private ObjectResult Problema(int status, string title, string code)
    {
        return new ObjectResult(new
        {
            status,
            title,
            code,
            traceId = HttpContext.TraceIdentifier,
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}

public sealed class FiltroSemCache : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext contexto, ResultExecutionDelegate proximo)
    {
        contexto.HttpContext.Response.Headers.CacheControl = "no-store";
        await proximo();
    }
}
