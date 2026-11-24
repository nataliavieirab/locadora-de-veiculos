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
public sealed class AlugueisController(CasosEdicaoAluguel edicao, CasosConsultaAluguel consulta,
    CasosEstimativas estimativas, CasosContratacao contratacao) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancelamento, int page = 1, int pageSize = 20,
        string? estado = null, Guid? veiculoId = null, string? saidaDe = null, string? saidaAte = null)
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
            saidaAte is null ? null : InstanteTransporte.Ler(saidaAte, "saidaAte"));
        var pagina = await consulta.ListarAsync(new Paginacao(page, pageSize), filtro, cancelamento);
        return Ok(new Pagina<SaidaItemListaAluguel>(pagina.Items.Select(SaidaItemListaAluguel.De).ToArray(),
            pagina.Page, pagina.PageSize, pagina.Total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelamento)
    {
        var resultado = await consulta.ConsultarAsync(id, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(resultado.RowVersion);
        return Ok(SaidaContrato.De(resultado.Revisao));
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
    {
        if (!headers.TryGetValue("Idempotency-Key", out var valores)
            || !Guid.TryParse(valores.FirstOrDefault(), out var chave))
        {
            throw new ValidacaoCadastroException("idempotencyKey",
                "Informe o cabeçalho Idempotency-Key com um GUID válido.");
        }

        return ValidacaoCadastro.Referencia(chave, "idempotencyKey");
    }
}
