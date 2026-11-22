using Locadora.Application.Acesso.Registro;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Locadora.Api.Acesso;

public sealed class EntradaRegistro
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public Guid? EmpresaId { get; set; }
}

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("registro")]
public sealed class RegistroController : ControllerBase
{
    private readonly CasoRegistroEmpresa _caso;

    public RegistroController(CasoRegistroEmpresa caso)
    {
        _caso = caso;
    }

    [HttpPost("/api/v1/registro")]
    public async Task<IActionResult> Registrar([FromBody] EntradaRegistro? entrada, CancellationToken cancelamento)
    {
        var resultado = await _caso.ExecutarAsync(entrada?.Username, entrada?.Email, entrada?.Password, cancelamento);
        return resultado switch
        {
            ResultadoRegistro.Criado criado => StatusCode(
                StatusCodes.Status201Created,
                new { empresaId = criado.EmpresaId, contaId = criado.ContaId }),
            ResultadoRegistro.Invalido invalido => Problema(
                StatusCodes.Status400BadRequest,
                "Requisição inválida",
                "validacao",
                invalido.Erros),
            ResultadoRegistro.Conflito conflito => Problema(
                StatusCodes.Status409Conflict,
                "Conflito",
                "usuario_existente",
                [new ErroCampo(conflito.Campo, conflito.Mensagem)]),
            _ => Problema(StatusCodes.Status500InternalServerError, "Erro interno", "erro_interno", null),
        };
    }

    private ObjectResult Problema(int status, string title, string code, IReadOnlyList<ErroCampo>? erros)
    {
        IReadOnlyDictionary<string, string[]>? mapa = erros is null
            ? null
            : erros.GroupBy(erro => erro.Campo)
                .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(erro => erro.Mensagem).ToArray());
        return new ObjectResult(new
        {
            status,
            title,
            code,
            traceId = HttpContext.TraceIdentifier,
            errors = mapa,
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
