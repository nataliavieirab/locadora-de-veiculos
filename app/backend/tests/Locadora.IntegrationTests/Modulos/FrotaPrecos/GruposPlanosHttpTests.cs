using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;

namespace Locadora.IntegrationTests.Modulos.FrotaPrecos;

[TestClass]
public sealed class GruposPlanosHttpTests
{
    [TestMethod]
    public async Task TresModalidadesRetornamStringsDecimaisEValoresNaListaDoGrupo()
    {
        using var cliente = await AutenticadoAsync();
        var grupo = await Criar(cliente, "/api/v1/grupos", new { nome = "Grupo sintético" });
        var diario = await Criar(cliente, "/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Diario", precoDiario = "100.00", precoKm = "1.123456" });
        await Criar(cliente, "/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Controlado", precoDiario = "150", franquiaDiariaKm = 100, precoKmExcedente = "2.50" });
        await Criar(cliente, "/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Livre", precoDiario = "200.00" });
        Assert.AreEqual(JsonValueKind.String, diario.Json.GetProperty("precoDiario").ValueKind);
        Assert.AreEqual("1.123456", diario.Json.GetProperty("precoKm").GetString());
        using var lista = await cliente.GetAsync("/api/v1/grupos");
        var json = await ApiCliente.JsonAsync(lista);
        Assert.AreEqual(1, json.GetProperty("total").GetInt32());
        Assert.AreEqual(3, json.GetProperty("items")[0].GetProperty("planos").GetArrayLength());
        using var duplicado = await cliente.PostAsJsonAsync("/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Livre", precoDiario = "0" });
        Assert.AreEqual(HttpStatusCode.Conflict, duplicado.StatusCode);
        using var bloqueado = await Mutar(cliente, HttpMethod.Delete, $"/api/v1/grupos/{grupo.Id}", grupo.Etag);
        Assert.AreEqual(HttpStatusCode.Conflict, bloqueado.StatusCode);
    }

    [TestMethod]
    public async Task ValoresInvalidosNaoSaoArredondadosNemCriamPlano()
    {
        using var cliente = await AutenticadoAsync();
        var grupo = await Criar(cliente, "/api/v1/grupos", new { nome = "Grupo" });
        foreach (var preco in new[] { "-1", "1.0000000", "10000000000000", "1,25", "1e3" })
        {
            using var resposta = await cliente.PostAsJsonAsync("/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Livre", precoDiario = preco });
            Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        }
        using var parametroExtra = await cliente.PostAsJsonAsync("/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Livre", precoDiario = "100", precoKm = "1" });
        Assert.AreEqual(HttpStatusCode.BadRequest, parametroExtra.StatusCode);
        using var numero = await cliente.PostAsJsonAsync("/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Livre", precoDiario = 100 });
        Assert.AreEqual(HttpStatusCode.BadRequest, numero.StatusCode);
        using var lista = await cliente.GetAsync("/api/v1/planos");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(lista)).GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task EdicaoEExclusaoLogicaRespeitamEtagEVinculos()
    {
        using var cliente = await AutenticadoAsync();
        var grupo = await Criar(cliente, "/api/v1/grupos", new { nome = "Original" });
        var plano = await Criar(cliente, "/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Livre", precoDiario = "100" });
        using var semVersao = await cliente.PutAsJsonAsync($"/api/v1/grupos/{grupo.Id}", new { nome = "Atual" });
        Assert.AreEqual(428, (int)semVersao.StatusCode);
        using var atualiza = await Mutar(cliente, HttpMethod.Put, $"/api/v1/grupos/{grupo.Id}", grupo.Etag, new { nome = "Atual" });
        Assert.AreEqual(HttpStatusCode.OK, atualiza.StatusCode);
        using var obsoleta = await Mutar(cliente, HttpMethod.Delete, $"/api/v1/grupos/{grupo.Id}", grupo.Etag);
        Assert.AreEqual(412, (int)obsoleta.StatusCode);
        using var alteraPlano = await Mutar(cliente, HttpMethod.Put, $"/api/v1/planos/{plano.Id}", plano.Etag, new { precoDiario = "120" });
        Assert.AreEqual(HttpStatusCode.OK, alteraPlano.StatusCode);
        Assert.AreEqual("120.00", (await ApiCliente.JsonAsync(alteraPlano)).GetProperty("precoDiario").GetString());
        using var excluiPlano = await Mutar(cliente, HttpMethod.Delete, $"/api/v1/planos/{plano.Id}", alteraPlano.Headers.ETag!.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, excluiPlano.StatusCode);
        using var excluiGrupo = await Mutar(cliente, HttpMethod.Delete, $"/api/v1/grupos/{grupo.Id}", atualiza.Headers.ETag!.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, excluiGrupo.StatusCode);
        using var lista = await cliente.GetAsync("/api/v1/grupos");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(lista)).GetProperty("total").GetInt32());
        var empresa = ApiCliente.ClaimGuid(ApiCliente.LerToken(cliente.DefaultRequestHeaders.Authorization!.Parameter!), "empresaId");
        await using var banco = AmbienteIntegracao.CriarContexto(contexto: new ContextoEmpresa { EmpresaId = empresa });
        Assert.IsTrue(await banco.Grupos.IgnoreQueryFilters().AnyAsync(item => item.EmpresaId == empresa && item.Id == grupo.Id && item.ExcluidoEmUtc != null));
        Assert.IsTrue(await banco.Planos.IgnoreQueryFilters().AnyAsync(item => item.EmpresaId == empresa && item.Id == plano.Id && item.ExcluidoEmUtc != null));
        Assert.AreEqual(3, await banco.AuditoriasCadastro.CountAsync(item => item.EntidadeId == grupo.Id));
        Assert.AreEqual(3, await banco.AuditoriasCadastro.CountAsync(item => item.EntidadeId == plano.Id));
    }

    [TestMethod]
    public async Task VeiculoAtivoTambemBloqueiaExclusaoDoGrupoSemAuditoriaParcial()
    {
        using var cliente = await AutenticadoAsync();
        var grupo = await Criar(cliente, "/api/v1/grupos", new { nome = "Grupo com veículo" });
        var empresa = ApiCliente.ClaimGuid(ApiCliente.LerToken(cliente.DefaultRequestHeaders.Authorization!.Parameter!), "empresaId");
        await using var banco = AmbienteIntegracao.CriarContextoComGuarda(new ContextoEmpresa { EmpresaId = empresa });
        banco.Veiculos.Add(Veiculo.Novo(empresa,
            new DadosVeiculo("ABC-1D23", "Marca sintética", "Azul", "Modelo sintético", TipoCombustivel.Gasolina, 50m, 2026, grupo.Id),
            DateTimeOffset.UtcNow));
        await banco.SaveChangesAsync();

        using var resposta = await Mutar(cliente, HttpMethod.Delete, $"/api/v1/grupos/{grupo.Id}", grupo.Etag);

        Assert.AreEqual(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.IsTrue(await banco.Grupos.AnyAsync(item => item.Id == grupo.Id));
        Assert.AreEqual(1, await banco.AuditoriasCadastro.CountAsync(item => item.EntidadeId == grupo.Id));
        Assert.IsFalse(await banco.AuditoriasCadastro.AnyAsync(item => item.EntidadeId == grupo.Id && item.Acao == AcaoCadastro.Exclusao));
    }

    [TestMethod]
    public async Task EmpresaNaoAcessaGrupoPlanoOuAssociacaoExterna()
    {
        using var a = await AutenticadoAsync();
        using var b = await AutenticadoAsync();
        var grupoB = await Criar(b, "/api/v1/grupos", new { nome = "Empresa B" });
        var planoB = await Criar(b, "/api/v1/planos", new { grupoId = grupoB.Id, modalidade = "Livre", precoDiario = "10" });
        using var grupo = await a.GetAsync($"/api/v1/grupos/{grupoB.Id}");
        using var plano = await a.GetAsync($"/api/v1/planos/{planoB.Id}");
        using var criacao = await a.PostAsJsonAsync("/api/v1/planos", new { grupoId = grupoB.Id, modalidade = "Diario", precoDiario = "10", precoKm = "1" });
        using var alteracao = await Mutar(a, HttpMethod.Put, $"/api/v1/planos/{planoB.Id}", planoB.Etag, new { precoDiario = "20" });
        Assert.AreEqual(HttpStatusCode.NotFound, grupo.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, plano.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, criacao.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, alteracao.StatusCode);
        using var lista = await a.GetAsync("/api/v1/grupos");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(lista)).GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task ModalidadeExcluidaPodeSerConfiguradaComNovaIdentidadeSemAlterarHistorico()
    {
        using var cliente = await AutenticadoAsync();
        var grupo = await Criar(cliente, "/api/v1/grupos", new { nome = "Grupo para reconfiguração" });
        var anterior = await Criar(cliente, "/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Livre", precoDiario = "100" });
        using var exclusao = await Mutar(cliente, HttpMethod.Delete, $"/api/v1/planos/{anterior.Id}", anterior.Etag);
        Assert.AreEqual(HttpStatusCode.NoContent, exclusao.StatusCode);

        var novo = await Criar(cliente, "/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Livre", precoDiario = "120" });
        Assert.AreNotEqual(anterior.Id, novo.Id);
        using var duplicado = await cliente.PostAsJsonAsync("/api/v1/planos", new { grupoId = grupo.Id, modalidade = "Livre", precoDiario = "150" });
        Assert.AreEqual(HttpStatusCode.Conflict, duplicado.StatusCode);
        using var leituraAnterior = await cliente.GetAsync($"/api/v1/planos/{anterior.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, leituraAnterior.StatusCode);
        using var leituraGrupo = await cliente.GetAsync($"/api/v1/grupos/{grupo.Id}");
        var planos = (await ApiCliente.JsonAsync(leituraGrupo)).GetProperty("planos");
        Assert.AreEqual(1, planos.GetArrayLength());
        Assert.AreEqual(novo.Id, planos[0].GetProperty("id").GetGuid());

        var empresa = ApiCliente.ClaimGuid(ApiCliente.LerToken(cliente.DefaultRequestHeaders.Authorization!.Parameter!), "empresaId");
        await using var banco = AmbienteIntegracao.CriarContexto(contexto: new ContextoEmpresa { EmpresaId = empresa });
        var historico = await banco.Planos.IgnoreQueryFilters()
            .SingleAsync(item => item.EmpresaId == empresa && item.Id == anterior.Id);
        Assert.IsNotNull(historico.ExcluidoEmUtc);
        Assert.AreEqual(100m, historico.PrecoDiario);
        Assert.AreEqual(grupo.Id, historico.GrupoId);
        Assert.AreEqual(2, await banco.AuditoriasCadastro.CountAsync(item => item.EntidadeId == anterior.Id));
        Assert.AreEqual(1, await banco.AuditoriasCadastro.CountAsync(item => item.EntidadeId == novo.Id));
        Assert.IsFalse(await banco.AuditoriasCadastro.AnyAsync(item => item.EntidadeId == novo.Id && item.Acao != AcaoCadastro.Criacao));
    }

    private static async Task<HttpClient> AutenticadoAsync()
    {
        var cliente = Clientes.Criar();
        var username = "grupos_" + Guid.NewGuid().ToString("N");
        using var registro = await ApiCliente.RegistrarAsync(cliente, username, "teste@example.invalid", "Senha123");
        Assert.AreEqual(HttpStatusCode.Created, registro.StatusCode);
        using var login = await ApiCliente.EntrarAsync(cliente, username, "Senha123");
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        ApiCliente.Bearer(cliente, (await ApiCliente.JsonAsync(login)).GetProperty("accessToken").GetString()!);
        return cliente;
    }

    private static async Task<(Guid Id, string Etag, JsonElement Json)> Criar(HttpClient cliente, string rota, object entrada)
    {
        using var resposta = await cliente.PostAsJsonAsync(rota, entrada);
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        Assert.IsNotNull(resposta.Headers.Location);
        var json = await ApiCliente.JsonAsync(resposta);
        return (json.GetProperty("id").GetGuid(), resposta.Headers.ETag!.ToString(), json);
    }

    private static async Task<HttpResponseMessage> Mutar(HttpClient cliente, HttpMethod metodo, string rota, string etag, object? corpo = null)
    {
        using var pedido = new HttpRequestMessage(metodo, rota);
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        if (corpo is not null) pedido.Content = JsonContent.Create(corpo);
        return await cliente.SendAsync(pedido);
    }
}
