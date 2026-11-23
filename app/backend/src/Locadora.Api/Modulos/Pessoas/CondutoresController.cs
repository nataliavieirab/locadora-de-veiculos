using Locadora.Api.Host;
using Locadora.Application.Modulos.Pessoas;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Pessoas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Modulos.Pessoas;

[ApiController]
[Authorize]
[ServiceFilter(typeof(FiltroSemCache))]
[Route("/api/v1/condutores")]
public sealed class CondutoresController(CasosPessoas casos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Pagina<SaidaCondutor>>> Listar(CancellationToken cancelamento, int page = 1, int pageSize = 20)
    {
        var pagina = await casos.ListarCondutoresAsync(new Paginacao(page, pageSize), cancelamento);
        var itens = new List<SaidaCondutor>();
        foreach (var condutor in pagina.Items)
        {
            itens.Add(await Mapear(condutor, cancelamento));
        }
        return Ok(new Pagina<SaidaCondutor>(itens, pagina.Page, pagina.PageSize, pagina.Total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelamento)
    {
        var condutor = await casos.CondutorAsync(id, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(condutor.RowVersion);
        return Ok(await Mapear(condutor, cancelamento));
    }

    [HttpPost]
    public async Task<IActionResult> Criar(EntradaCondutor entrada, CancellationToken cancelamento)
    {
        var condutor = await casos.CriarCondutorAsync(entrada.ParaDominio(), entrada.ProprioCliente, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(condutor.RowVersion);
        Response.Headers.Location = $"/api/v1/condutores/{condutor.Id}";
        return StatusCode(201, await Mapear(condutor, cancelamento));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Alterar(Guid id, EntradaCondutor entrada, CancellationToken cancelamento)
    {
        var condutor = await casos.AlterarCondutorAsync(id, entrada.ParaDominio(), entrada.ProprioCliente, Request.Headers.IfMatch, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(condutor.RowVersion);
        return Ok(await Mapear(condutor, cancelamento));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancelamento)
    {
        await casos.ExcluirCondutorAsync(id, Request.Headers.IfMatch, cancelamento);
        return NoContent();
    }

    private async Task<SaidaCondutor> Mapear(Condutor condutor, CancellationToken cancelamento) =>
        SaidasPessoas.Condutor(condutor, await casos.ClienteAsync(condutor.ClienteId, cancelamento));
}
