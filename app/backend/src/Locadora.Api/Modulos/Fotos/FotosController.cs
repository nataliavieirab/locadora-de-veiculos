using Locadora.Api.Host;
using Locadora.Application.Modulos.Fotos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Fotos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Modulos.Fotos;

public sealed record SaidaFoto(Guid Id, string Caminho, string Mime, int TamanhoBytes, int Largura, int Altura)
{
    public static SaidaFoto? De(FotoObjeto? foto) => foto is null ? null
        : new(foto.Id, foto.Caminho, foto.Mime, foto.TamanhoBytes, foto.Largura, foto.Altura);
}
public sealed record SaidaMutacaoFoto(SaidaFoto Foto);

[ApiController, Authorize, ServiceFilter(typeof(FiltroSemCache))]
[Route("/api/v1/veiculos/{id:guid}/foto")]
public sealed class FotosController(CasosFotos casos, ConsultaFotos consultas, IRepositorioFotos fotos) : ControllerBase
{
    [HttpPut, Consumes("multipart/form-data"), SemBindingFormulario, RequestSizeLimit(LeitorMultipartFoto.MaximoCorpo)]
    [ProducesResponseType<SaidaMutacaoFoto>(200), ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403)]
    [ProducesResponseType(404), ProducesResponseType(409), ProducesResponseType(412), ProducesResponseType(413)]
    [ProducesResponseType(415), ProducesResponseType(428), ProducesResponseType(503)]
    public async Task<IActionResult> Enviar(Guid id, CancellationToken cancelamento)
    {
        await casos.AutorizarAsync(id, Request.Headers.IfMatch, cancelamento);
        var (arquivo, mime) = await LeitorMultipartFoto.LerAsync(Request, cancelamento);
        using (arquivo)
        {
            var veiculo = await casos.EnviarAsync(id, Request.Headers.IfMatch, arquivo, mime, cancelamento);
            var foto = await fotos.ConsultarAsync(veiculo.FotoAtivaId!.Value, cancelamento);
            Response.Headers.ETag = VersaoCadastro.Etag(veiculo.RowVersion);
            Response.Headers.Location = $"/api/v1/veiculos/{id}/foto";
            return Ok(new SaidaMutacaoFoto(SaidaFoto.De(foto)!));
        }
    }

    [HttpDelete]
    [ProducesResponseType(204), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(404)]
    [ProducesResponseType(409), ProducesResponseType(412), ProducesResponseType(428), ProducesResponseType(503)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancelamento)
    {
        var veiculo = await casos.RemoverAsync(id, Request.Headers.IfMatch, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(veiculo.RowVersion);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(200), ProducesResponseType(401), ProducesResponseType(403)]
    [ProducesResponseType(404), ProducesResponseType(503)]
    public async Task<IActionResult> Ler(Guid id, CancellationToken cancelamento)
    {
        var (foto, bytes) = await consultas.LerAsync(id, cancelamento);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        Response.ContentLength = bytes.Length;
        return File(new MemoryStream(bytes, writable: false), foto.Mime, enableRangeProcessing: false);
    }
}
