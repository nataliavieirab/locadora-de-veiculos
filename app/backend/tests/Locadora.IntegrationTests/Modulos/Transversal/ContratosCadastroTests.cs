using System.Net;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Locadora.IntegrationTests.Modulos.Transversal;

[TestClass]
public sealed class ContratosCadastroTests
{
    [TestMethod]
    [DataRow("PUT")]
    [DataRow("DELETE")]
    public async Task MutacaoSemIfMatchRetornaProblem428SemGravar(string metodo)
    {
        var (fabrica, id, empresaId) = await CriarAsync();
        await using var escopo = fabrica;
        using var cliente = fabrica.CreateClient();
        using var pedido = new HttpRequestMessage(new HttpMethod(metodo), $"/__test/cadastro/{id}");
        using var resposta = await cliente.SendAsync(pedido);

        await ProblemaAsync(resposta, 428, "versao_ausente");
        await using var banco = AmbienteIntegracao.CriarContexto(contexto: new ContextoEmpresa { EmpresaId = empresaId });
        Assert.AreEqual("Inicial", (await banco.Grupos.SingleAsync()).Nome);
    }

    [TestMethod]
    public async Task EtagDoSqlPermiteEdicaoMasTelaObsoletaRecebe412()
    {
        var (fabrica, id, _) = await CriarAsync();
        await using var escopo = fabrica;
        using var cliente = fabrica.CreateClient();
        using var consulta = await cliente.GetAsync($"/__test/cadastro/{id}");
        Assert.AreEqual(HttpStatusCode.OK, consulta.StatusCode);
        var etag = consulta.Headers.ETag!.ToString();
        using var edicao = new HttpRequestMessage(HttpMethod.Put, $"/__test/cadastro/{id}");
        edicao.Headers.TryAddWithoutValidation("If-Match", etag);
        using var resposta = await cliente.SendAsync(edicao);
        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        Assert.AreNotEqual(etag, resposta.Headers.ETag!.ToString());
        using var antiga = new HttpRequestMessage(HttpMethod.Put, $"/__test/cadastro/{id}");
        antiga.Headers.TryAddWithoutValidation("If-Match", etag);
        using var conflito = await cliente.SendAsync(antiga);
        await ProblemaAsync(conflito, 412, "versao_divergente");
        using var exclusao = new HttpRequestMessage(HttpMethod.Delete, $"/__test/cadastro/{id}");
        exclusao.Headers.TryAddWithoutValidation("If-Match", resposta.Headers.ETag!.ToString());
        using var excluida = await cliente.SendAsync(exclusao);
        Assert.AreEqual(HttpStatusCode.NoContent, excluida.StatusCode);
    }

    [TestMethod]
    public async Task ConstraintSqlVira409SemDetalhesTecnicosEComRollback()
    {
        var (fabrica, _, empresaId) = await CriarAsync();
        await using var escopo = fabrica;
        using var cliente = fabrica.CreateClient();
        using var resposta = await cliente.PostAsync("/__test/cadastro/conflito", null);
        await ProblemaAsync(resposta, 409, "cadastro_conflito");
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(texto, "SqlException");
        Assert.DoesNotContain(texto, "INSERT");
        Assert.DoesNotContain(texto, "IX_PrecosSeguros");
        await using var banco = AmbienteIntegracao.CriarContexto(contexto: new ContextoEmpresa { EmpresaId = empresaId });
        Assert.AreEqual(0, await banco.PrecosSeguros.CountAsync());
    }

    private static async Task ProblemaAsync(HttpResponseMessage resposta, int status, string codigo)
    {
        Assert.AreEqual(status, (int)resposta.StatusCode);
        Assert.AreEqual("application/problem+json", resposta.Content.Headers.ContentType!.MediaType);
        var json = await ApiCliente.JsonAsync(resposta);
        Assert.AreEqual(status, json.GetProperty("status").GetInt32());
        Assert.AreEqual(codigo, json.GetProperty("code").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(json.GetProperty("title").GetString()));
        Assert.IsFalse(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
        Assert.IsTrue(ApiCliente.NoStore(resposta));
    }

    private static async Task<(FabricaApi Fabrica, Guid Id, Guid EmpresaId)> CriarAsync()
    {
        var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true };
        await using var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto);
        var empresa = Empresa.Nova();
        contexto.EmpresaId = empresa.Id;
        var grupo = Grupo.Novo(empresa.Id, "Inicial", DateTimeOffset.UtcNow);
        banco.AddRange(empresa, grupo);
        await banco.SaveChangesAsync();
        var fabrica = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave, servicos: servicos =>
        {
            servicos.AddScoped(_ => new ContextoEmpresa { EmpresaId = empresa.Id });
            servicos.AddControllers().AddApplicationPart(typeof(ContratoCadastroTesteController).Assembly);
        });
        return (fabrica, grupo.Id, empresa.Id);
    }
}

// Este controller só é registrado pela fábrica acima; não pertence à aplicação.
[ApiController]
[Route("/__test/cadastro")]
public sealed class ContratoCadastroTesteController(
    LocadoraDbContext banco, ContextoEmpresa contexto, IUnidadeTrabalhoEmpresa unidade) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken cancelamento)
    {
        var grupo = await banco.Grupos.SingleAsync(item => item.Id == id, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(grupo.RowVersion);
        return Ok(new { grupo.Id, grupo.Nome });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Alterar(Guid id, CancellationToken cancelamento)
    {
        var grupo = await unidade.ExecutarAsync(async token =>
        {
            var atual = await banco.Grupos.SingleAsync(item => item.Id == id, token);
            VersaoCadastro.Exigir(Request.Headers.IfMatch, atual.RowVersion);
            atual.Alterar("Atualizado", DateTimeOffset.UtcNow);
            return atual;
        }, cancelamento);
        Response.Headers.ETag = VersaoCadastro.Etag(grupo.RowVersion);
        return Ok(new { grupo.Id, grupo.Nome });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancelamento)
    {
        await unidade.ExecutarAsync(async token =>
        {
            var grupo = await banco.Grupos.SingleAsync(item => item.Id == id, token);
            VersaoCadastro.Exigir(Request.Headers.IfMatch, grupo.RowVersion);
            grupo.Excluir(DateTimeOffset.UtcNow);
            return true;
        }, cancelamento);
        return NoContent();
    }

    [HttpPost("conflito")]
    public async Task<IActionResult> Conflito(CancellationToken cancelamento)
    {
        await unidade.ExecutarAsync(token =>
        {
            banco.AddRange(
                PrecoSeguro.Novo(contexto.EmpresaId!.Value, TipoSeguro.Cliente, 0m, DateTimeOffset.UtcNow),
                PrecoSeguro.Novo(contexto.EmpresaId.Value, TipoSeguro.Cliente, 1m, DateTimeOffset.UtcNow));
            return Task.FromResult(true);
        }, cancelamento);
        return Ok();
    }
}
