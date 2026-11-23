using Locadora.Api.Host;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Modulos.FrotaPrecos;

[ApiController]
[Authorize]
[ServiceFilter(typeof(FiltroSemCache))]
[Route("/api/v1/configuracoes/seguros/{tipo}")]
public sealed class PrecosSegurosController(CasosPrecosSeguros casos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar(string tipo, CancellationToken cancelamento)
    {
        var modalidade = TiposConfiguracao.Seguro(tipo);
        var seguro = await casos.SeguroAsync(modalidade, cancelamento);
        if (seguro is not null) Response.Headers.ETag = VersaoCadastro.Etag(seguro.RowVersion);
        return Ok(SaidaPrecoSeguro.De(modalidade, seguro));
    }

    [HttpPut]
    public async Task<IActionResult> Salvar(string tipo, EntradaPrecoSeguro entrada, CancellationToken cancelamento)
    {
        var modalidade = TiposConfiguracao.Seguro(tipo);
        var versao = Request.Headers.IfMatch.ToString();
        var seguro = await casos.SalvarAsync(modalidade, DecimalTransporte.Ler(entrada.PrecoDiario, "precoDiario"),
            versao, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(seguro.RowVersion);
        if (string.IsNullOrWhiteSpace(versao))
        {
            Response.Headers.Location = $"/api/v1/configuracoes/seguros/{modalidade}";
            return StatusCode(201, SaidaPrecoSeguro.De(modalidade, seguro));
        }
        return Ok(SaidaPrecoSeguro.De(modalidade, seguro));
    }
}
