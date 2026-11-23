using Locadora.Api.Host;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Modulos.FrotaPrecos;

[ApiController]
[Authorize]
[ServiceFilter(typeof(FiltroSemCache))]
[Route("/api/v1/grupos")]
public sealed class GruposController(CasosGruposPlanos casos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancelamento, int page = 1, int pageSize = 20)
    {
        var pagina = await casos.ListarGruposAsync(new Paginacao(page, pageSize), cancelamento);
        var itens = new List<SaidaGrupo>();
        foreach (var grupo in pagina.Items) itens.Add(await Mapear(grupo, cancelamento));
        return Ok(new Pagina<SaidaGrupo>(itens, pagina.Page, pagina.PageSize, pagina.Total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelamento)
    {
        var grupo = await casos.GrupoAsync(id, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(grupo.RowVersion);
        return Ok(await Mapear(grupo, cancelamento));
    }

    [HttpPost]
    public async Task<IActionResult> Criar(EntradaGrupo entrada, CancellationToken cancelamento)
    {
        var grupo = await casos.CriarGrupoAsync(entrada.Nome!, cancelamento);
        Response.Headers.Location = $"/api/v1/grupos/{grupo.Id}";
        Response.Headers.ETag = VersaoCadastro.Etag(grupo.RowVersion);
        return StatusCode(201, await Mapear(grupo, cancelamento));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Alterar(Guid id, EntradaGrupo entrada, CancellationToken cancelamento)
    {
        var grupo = await casos.AlterarGrupoAsync(id, entrada.Nome!, Request.Headers.IfMatch, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(grupo.RowVersion);
        return Ok(await Mapear(grupo, cancelamento));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancelamento)
    {
        await casos.ExcluirGrupoAsync(id, Request.Headers.IfMatch, cancelamento);
        return NoContent();
    }

    private async Task<SaidaGrupo> Mapear(Grupo grupo, CancellationToken cancelamento) =>
        new(grupo.Id, grupo.Nome, (await casos.PlanosDoGrupoAsync(grupo.Id, cancelamento)).Select(SaidaPlano.De).ToArray());
}
