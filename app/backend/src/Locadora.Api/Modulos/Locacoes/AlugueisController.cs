using Locadora.Api.Host;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Modulos.Locacoes;

[ApiController]
[Authorize]
[ServiceFilter(typeof(FiltroSemCache))]
[Route("/api/v1/alugueis")]
[ProducesResponseType(401)]
[ProducesResponseType(403)]
[ProducesResponseType(503)]
public sealed class AlugueisController(CasosEdicaoAluguel edicao, CasosConsultaAluguel consulta,
    CasosEstimativas estimativas, CasosContratacao contratacao,
    IOperacoesDevolucao devolucao, IOcultacaoAluguel ocultacao) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Pagina<SaidaItemListaAluguel>>(200)]
    public async Task<IActionResult> Listar(CancellationToken cancelamento, int page = 1, int pageSize = 20,
        string? estado = null, Guid? veiculoId = null, string? saidaDe = null, string? saidaAte = null,
        bool incluirOcultos = false)
    {
        EstadoAluguel? estadoFiltro = null;
        if (estado is not null)
        {
            if (!Enum.TryParse(estado, out EstadoAluguel parseado))
            {
                throw new ValidacaoCadastroException("estado", "Informe um estado válido para o recorte atual.");
            }

            estadoFiltro = parseado;
        }

        var filtro = new FiltroConsultaAlugueis(estadoFiltro, veiculoId,
            saidaDe is null ? null : InstanteTransporte.Ler(saidaDe, "saidaDe"),
            saidaAte is null ? null : InstanteTransporte.Ler(saidaAte, "saidaAte"), incluirOcultos);
        var pagina = await consulta.ListarAsync(new Paginacao(page, pageSize), filtro, cancelamento);
        return Ok(new Pagina<SaidaItemListaAluguel>(pagina.Items.Select(SaidaItemListaAluguel.De).ToArray(),
            pagina.Page, pagina.PageSize, pagina.Total));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SaidaContrato>(200)]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelamento, bool incluirOcultos = false)
    {
        var resultado = await consulta.ConsultarAsync(id, incluirOcultos, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(resultado.RowVersion);
        return Ok(SaidaContrato.De(resultado));
    }

    [HttpGet("referencias-veiculos")]
    [ProducesResponseType<Pagina<ReferenciaVeiculoHistorico>>(200)]
    public async Task<IActionResult> ReferenciasVeiculos(CancellationToken cancelamento, int page = 1,
        int pageSize = 20, bool incluirOcultos = false)
        => Ok(await consulta.ReferenciasVeiculosAsync(new Paginacao(page, pageSize), incluirOcultos, cancelamento));

    [HttpPost("{id:guid}/devolucao/previa")]
    [ProducesResponseType<SaidaPreviaDevolucao>(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(412)]
    [ProducesResponseType(428)]
    public async Task<IActionResult> PreviaDevolucao(Guid id, EntradaDevolucao entrada, CancellationToken cancelamento,
        [FromHeader(Name = "If-Match")] string? versao = null)
    {
        var resultado = await devolucao.PreviaAsync(id, entrada.ParaComando(), versao, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(resultado.RowVersion);
        return Ok(SaidaPreviaDevolucao.De(resultado));
    }

    [HttpPost("{id:guid}/devolucao")]
    [ProducesResponseType<SaidaDevolucao>(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(412)]
    [ProducesResponseType(428)]
    public async Task<IActionResult> Devolver(Guid id, EntradaConclusaoDevolucao entrada, CancellationToken cancelamento,
        [FromHeader(Name = "If-Match")] string? versao = null,
        [FromHeader(Name = "Idempotency-Key")] string? chave = null)
    {
        var resultado = await devolucao.ConcluirAsync(id, entrada.ParaConclusao(), versao,
            LerChaveIdempotente(chave), cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(resultado.RowVersion);
        return Ok(new SaidaDevolucao(resultado.AluguelId, "Concluido", SaidaFinalDevolucao.De(resultado.Demonstrativo)));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(412)]
    [ProducesResponseType(428)]
    public async Task<IActionResult> Ocultar(Guid id, CancellationToken cancelamento,
        [FromHeader(Name = "If-Match")] string? versao = null)
    {
        await ocultacao.OcultarAsync(id, versao, cancelamento);
        return NoContent();
    }

    [HttpPost("estimativas")]
    public async Task<IActionResult> Estimar(EntradaEstimativa entrada, CancellationToken cancelamento)
    {
        var previa = await estimativas.EstimarAsync(entrada.ParaComando(), entrada.AluguelId,
            Request.Headers.IfMatch, cancelamento);
        return Ok(SaidaContrato.De(previa.Revisao) with { SaidaUtc = previa.SaidaUtc });
    }

    [HttpPost]
    public async Task<IActionResult> Criar(EntradaContratacao entrada, CancellationToken cancelamento)
    {
        var chave = LerChaveIdempotente(Request.Headers);
        var resultado = await contratacao.CriarAsync(entrada.ParaComando(), chave, cancelamento);
        Response.Headers.Location = $"/api/v1/alugueis/{resultado.AluguelId}";
        Response.Headers.ETag = VersaoCadastro.Etag(resultado.RowVersion);
        return StatusCode(StatusCodes.Status201Created, SaidaContrato.De(resultado.Revisao));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SaidaContrato>> Alterar(Guid id, EntradaContratacao entrada, CancellationToken cancelamento)
    {
        var resultado = await edicao.AlterarAsync(id, entrada.ParaComando(), Request.Headers.IfMatch, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(resultado.RowVersion);
        return Ok(SaidaContrato.De(resultado.Revisao));
    }

    private static Guid LerChaveIdempotente(IHeaderDictionary headers)
        => LerChaveIdempotente(headers["Idempotency-Key"].ToString());

    private static Guid LerChaveIdempotente(string? valor)
    {
        if (!Guid.TryParse(valor, out var chave))
        {
            throw new ValidacaoCadastroException("idempotencyKey",
                "Informe o cabeçalho Idempotency-Key com um GUID válido.");
        }

        return ValidacaoCadastro.Referencia(chave, "idempotencyKey");
    }
}
