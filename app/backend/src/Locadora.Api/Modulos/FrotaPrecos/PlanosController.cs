using Locadora.Api.Host;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Modulos.FrotaPrecos;

[ApiController]
[Authorize]
[ServiceFilter(typeof(FiltroSemCache))]
[Route("/api/v1/planos")]
public sealed class PlanosController(CasosGruposPlanos casos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancelamento, int page = 1, int pageSize = 20, Guid? grupoId = null)
    {
        var pagina = await casos.ListarPlanosAsync(new Paginacao(page, pageSize), grupoId, cancelamento);
        return Ok(new Pagina<SaidaPlano>(pagina.Items.Select(SaidaPlano.De).ToArray(), pagina.Page, pagina.PageSize, pagina.Total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelamento)
    {
        var plano = await casos.PlanoAsync(id, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(plano.RowVersion);
        return Ok(SaidaPlano.De(plano));
    }

    [HttpPost]
    public async Task<IActionResult> Criar(EntradaPlano entrada, CancellationToken cancelamento)
    {
        var plano = await casos.CriarPlanoAsync(entrada.GrupoId, entrada.LerModalidade(), entrada.ParaDominio(), cancelamento);
        Response.Headers.Location = $"/api/v1/planos/{plano.Id}";
        Response.Headers.ETag = VersaoCadastro.Etag(plano.RowVersion);
        return StatusCode(201, SaidaPlano.De(plano));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Alterar(Guid id, EntradaPrecosPlano entrada, CancellationToken cancelamento)
    {
        var plano = await casos.AlterarPlanoAsync(id, entrada.ParaDominio(), Request.Headers.IfMatch, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(plano.RowVersion);
        return Ok(SaidaPlano.De(plano));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancelamento)
    {
        await casos.ExcluirPlanoAsync(id, Request.Headers.IfMatch, cancelamento);
        return NoContent();
    }
}
