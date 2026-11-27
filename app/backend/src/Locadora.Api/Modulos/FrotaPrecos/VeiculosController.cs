using Locadora.Api.Host;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Fotos;
using Locadora.Api.Modulos.Fotos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Modulos.FrotaPrecos;

[ApiController]
[Authorize]
[ServiceFilter(typeof(FiltroSemCache))]
[Route("/api/v1/veiculos")]
public sealed class VeiculosController(CasosVeiculos casos, CasosSelecoesLocacao selecoes, ConsultaFotos fotos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancelamento, Guid? grupoId = null, int page = 1,
        int pageSize = 20, bool somenteDisponiveis = false, Guid? aluguelEmEdicaoId = null)
    {
        var paginacao = new Paginacao(page, pageSize);
        var pagina = somenteDisponiveis || aluguelEmEdicaoId is not null
            ? await selecoes.VeiculosAsync(paginacao, grupoId, somenteDisponiveis, aluguelEmEdicaoId, cancelamento)
            : await casos.ListarAsync(paginacao, grupoId, cancelamento);
        return Ok(new Pagina<SaidaVeiculo>(pagina.Items.Select(SaidaVeiculo.De).ToArray(),
            pagina.Page, pagina.PageSize, pagina.Total));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SaidaDetalheVeiculo>(200)]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelamento)
    {
        var detalhe = await fotos.ConsultarAsync(id, cancelamento);
        var veiculo = detalhe.Veiculo;
        Response.Headers.ETag = VersaoCadastro.Etag(veiculo.RowVersion);
        return Ok(new SaidaDetalheVeiculo(veiculo.Id, veiculo.Placa, veiculo.Marca, veiculo.Cor,
            veiculo.Modelo, veiculo.Combustivel.ToString(), DecimalTransporte.Escrever(veiculo.CapacidadeLitros),
            veiculo.Ano, veiculo.GrupoId, SaidaFoto.De(detalhe.Foto), detalhe.PossuiAluguelAberto));
    }

    [HttpPost]
    public async Task<IActionResult> Criar(EntradaVeiculo entrada, CancellationToken cancelamento)
    {
        var veiculo = await casos.CriarAsync(entrada.ParaDominio(), cancelamento);
        Response.Headers.Location = $"/api/v1/veiculos/{veiculo.Id}";
        Response.Headers.ETag = VersaoCadastro.Etag(veiculo.RowVersion);
        return StatusCode(201, SaidaVeiculo.De(veiculo));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Alterar(Guid id, EntradaVeiculo entrada, CancellationToken cancelamento)
    {
        var veiculo = await casos.AlterarAsync(id, entrada.ParaDominio(), Request.Headers.IfMatch, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(veiculo.RowVersion);
        return Ok(SaidaVeiculo.De(veiculo));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancelamento)
    {
        await casos.ExcluirAsync(id, Request.Headers.IfMatch, cancelamento);
        return NoContent();
    }
}
