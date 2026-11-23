using Locadora.Api.Host;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Modulos.FrotaPrecos;

[ApiController]
[Authorize]
[ServiceFilter(typeof(FiltroSemCache))]
[Route("/api/v1/taxas-servicos")]
public sealed class TaxasServicosController(CasosTaxasServicos casos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancelamento, int page = 1, int pageSize = 20)
    {
        var pagina = await casos.ListarAsync(new Paginacao(page, pageSize), cancelamento);
        return Ok(new Pagina<SaidaTaxaServico>(pagina.Items.Select(SaidaTaxaServico.De).ToArray(),
            pagina.Page, pagina.PageSize, pagina.Total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelamento)
    {
        var taxa = await casos.ConsultarAsync(id, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(taxa.RowVersion);
        return Ok(SaidaTaxaServico.De(taxa));
    }

    [HttpPost]
    public async Task<IActionResult> Criar(EntradaTaxaServico entrada, CancellationToken cancelamento)
    {
        var taxa = await casos.CriarAsync(entrada.ParaDominio(), cancelamento);
        Response.Headers.Location = $"/api/v1/taxas-servicos/{taxa.Id}";
        Response.Headers.ETag = VersaoCadastro.Etag(taxa.RowVersion);
        return StatusCode(201, SaidaTaxaServico.De(taxa));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Alterar(Guid id, EntradaTaxaServico entrada, CancellationToken cancelamento)
    {
        var taxa = await casos.AlterarAsync(id, entrada.ParaDominio(), Request.Headers.IfMatch, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(taxa.RowVersion);
        return Ok(SaidaTaxaServico.De(taxa));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancelamento)
    {
        await casos.ExcluirAsync(id, Request.Headers.IfMatch, cancelamento);
        return NoContent();
    }
}
