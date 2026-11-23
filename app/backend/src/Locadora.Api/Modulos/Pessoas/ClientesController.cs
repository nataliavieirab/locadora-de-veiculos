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
[Route("/api/v1/clientes")]
public sealed class ClientesController(CasosPessoas casos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Pagina<ResumoCliente>>> Listar(CancellationToken cancelamento, int page = 1, int pageSize = 20)
    {
        var pagina = await casos.ListarClientesAsync(new Paginacao(page, pageSize), cancelamento);
        return Ok(new Pagina<ResumoCliente>(pagina.Items.Select(SaidasPessoas.Resumo).ToArray(), pagina.Page, pagina.PageSize, pagina.Total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelamento) =>
        Responder(await casos.ClienteAsync(id, cancelamento), 200);

    [HttpPost]
    public async Task<IActionResult> Criar(EntradaCliente entrada, CancellationToken cancelamento)
    {
        var cliente = await casos.CriarClienteAsync(entrada.ParaDominio(), cancelamento);
        Response.Headers.Location = $"/api/v1/clientes/{cliente.Id}";
        return Responder(cliente, 201);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Alterar(Guid id, EntradaCliente entrada, CancellationToken cancelamento) =>
        Responder(await casos.AlterarClienteAsync(id, entrada.ParaDominio(), Request.Headers.IfMatch, cancelamento), 200);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancelamento)
    {
        await casos.ExcluirClienteAsync(id, Request.Headers.IfMatch, cancelamento);
        return NoContent();
    }

    [HttpGet("{id:guid}/vinculos-pj")]
    public async Task<IActionResult> Vinculos(Guid id, CancellationToken cancelamento)
    {
        var saidas = new List<SaidaVinculo>();
        foreach (var vinculo in await casos.VinculosAsync(id, cancelamento))
        {
            saidas.Add(await MapearVinculo(vinculo, cancelamento));
        }
        return Ok(saidas);
    }

    [HttpGet("{id:guid}/vinculos-pj/{vinculoId:guid}")]
    public async Task<IActionResult> Vinculo(Guid id, Guid vinculoId, CancellationToken cancelamento)
    {
        var vinculo = await casos.VinculoAsync(id, vinculoId, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(vinculo.RowVersion);
        return Ok(await MapearVinculo(vinculo, cancelamento));
    }

    [HttpPost("{id:guid}/vinculos-pj")]
    public async Task<IActionResult> Vincular(Guid id, EntradaVinculo entrada, CancellationToken cancelamento)
    {
        var vinculo = await casos.VincularAsync(id, entrada.OutroClienteId, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(vinculo.RowVersion);
        Response.Headers.Location = $"/api/v1/clientes/{id}/vinculos-pj/{vinculo.Id}";
        return StatusCode(201, await MapearVinculo(vinculo, cancelamento));
    }

    [HttpDelete("{id:guid}/vinculos-pj/{vinculoId:guid}")]
    public async Task<IActionResult> Desvincular(Guid id, Guid vinculoId, CancellationToken cancelamento)
    {
        await casos.DesvincularAsync(id, vinculoId, Request.Headers.IfMatch, cancelamento);
        return NoContent();
    }

    private ObjectResult Responder(Cliente cliente, int status)
    {
        Response.Headers.ETag = VersaoCadastro.Etag(cliente.RowVersion);
        return new ObjectResult(SaidasPessoas.Cliente(cliente)) { StatusCode = status, DeclaredType = typeof(SaidaCliente) };
    }

    private async Task<SaidaVinculo> MapearVinculo(VinculoCliente vinculo, CancellationToken cancelamento)
    {
        var pf = await casos.ClienteAsync(vinculo.ClientePfId, cancelamento);
        var pj = await casos.ClienteAsync(vinculo.ClientePjId, cancelamento);
        return new SaidaVinculo(vinculo.Id, pf.Id, pf.Nome, pj.Id, pj.Nome);
    }
}
