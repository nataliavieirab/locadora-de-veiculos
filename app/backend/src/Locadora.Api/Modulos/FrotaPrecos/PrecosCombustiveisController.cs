using Locadora.Api.Host;
using Locadora.Application.Modulos.FrotaPrecos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Modulos.FrotaPrecos;

[ApiController]
[Authorize]
[ServiceFilter(typeof(FiltroSemCache))]
[Route("/api/v1/configuracoes/combustiveis/{tipo}")]
public sealed class PrecosCombustiveisController(CasosPrecosCombustiveis casos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar(string tipo, CancellationToken cancelamento)
    {
        var modalidade = TiposConfiguracao.Combustivel(tipo);
        var historico = await casos.ListarAsync(modalidade, cancelamento);
        var atual = historico.LastOrDefault();
        return Ok(new SaidaPrecoCombustivel(modalidade.ToString(), atual is not null,
            atual is null ? null : SaidaVigenciaCombustivel.De(atual),
            historico.Reverse().Select(SaidaVigenciaCombustivel.De).ToArray()));
    }

    [HttpPut]
    public async Task<IActionResult> Registrar(string tipo, EntradaPrecoCombustivel entrada, CancellationToken cancelamento)
    {
        var modalidade = TiposConfiguracao.Combustivel(tipo);
        var preco = await casos.RegistrarAsync(modalidade,
            DecimalTransporte.Ler(entrada.PrecoUnitario, "precoUnitario"), cancelamento);
        Response.Headers.Location = $"/api/v1/configuracoes/combustiveis/{modalidade}";
        return StatusCode(201, SaidaVigenciaCombustivel.De(preco));
    }
}
