using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.IntegrationTests.Suporte;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Locadora.IntegrationTests.Modulos.Locacoes.PersistenciaDevolucaoSqlTests;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class DevolucaoHttpTests
{
    private FabricaApi _fabrica = null!;

    [TestInitialize]
    public void Iniciar() => _fabrica = new(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave, servicos: s =>
    {
        s.RemoveAll<TimeProvider>();
        s.AddSingleton<TimeProvider>(new Relogio());
    });

    [TestCleanup]
    public async Task Encerrar() => await _fabrica.DisposeAsync();

    private sealed class Relogio : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => CenarioLocacoesSql.Agora;
    }

    [TestMethod]
    public async Task PreviaFinalConsultaOcultacaoEReferenciasSaoAditivosComEstimativaPreservada()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(c);
        var dados = await PrepararDadosAsync(c);
        var etag = VersaoCadastro.Etag(aluguel.RowVersion);
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(c, _fabrica);
        var caminho = $"/api/v1/alugueis/{aluguel.Id}";
        using var aberto = await cliente.GetAsync(caminho);
        var contrato = await ApiCliente.JsonAsync(aberto);
        Assert.AreEqual("EmAberto", contrato.GetProperty("estado").GetString());
        Assert.AreEqual(JsonValueKind.Null, contrato.GetProperty("devolucao").ValueKind);
        var estimativa = contrato.GetProperty("estimativa").GetRawText();
        var entrada = Entrada(PreviaDevolucaoSqlTests.Comando(dados));
        using var previa = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao/previa", entrada, etag);
        Assert.AreEqual(HttpStatusCode.OK, previa.StatusCode);
        var p = await ApiCliente.JsonAsync(previa);
        Assert.AreEqual(etag, p.GetProperty("versaoAluguel").GetString());
        Assert.AreEqual(334m, decimal.Parse(p.GetProperty("calculo").GetProperty("totalFinal").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.AreEqual("1000.00", p.GetProperty("calculo").GetProperty("caucao").GetString());
        Assert.AreEqual("devolucao-v1", p.GetProperty("calculo").GetProperty("versaoRegra").GetString());
        Assert.AreEqual(0, await c.Banco.DemonstrativosFinais.CountAsync());
        entrada["versoesAceitas"] = JsonNode.Parse(p.GetProperty("versoesAceitas").GetRawText());
        using var final = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao", entrada, etag, Guid.NewGuid().ToString());
        Assert.AreEqual(HttpStatusCode.OK, final.StatusCode);
        Assert.AreNotEqual(etag, final.Headers.ETag?.ToString());
        var f = (await ApiCliente.JsonAsync(final)).GetProperty("devolucao");
        Assert.AreEqual(p.GetProperty("calculo").GetRawText(), f.GetProperty("calculo").GetRawText());
        using var consulta = await cliente.GetAsync(caminho);
        var concluido = await ApiCliente.JsonAsync(consulta);
        Assert.AreEqual("Concluido", concluido.GetProperty("estado").GetString());
        Assert.AreEqual(estimativa, concluido.GetProperty("estimativa").GetRawText());
        Assert.AreEqual(f.GetRawText(), concluido.GetProperty("devolucao").GetRawText());
        using var lista = await cliente.GetAsync("/api/v1/alugueis?estado=Concluido");
        var item = (await ApiCliente.JsonAsync(lista)).GetProperty("items").EnumerateArray().Single();
        Assert.AreEqual("334.00", item.GetProperty("totalFinal").GetString());
        Assert.IsFalse(item.GetProperty("oculto").GetBoolean());
        using var referencias = await cliente.GetAsync("/api/v1/alugueis/referencias-veiculos?pageSize=1");
        Assert.AreEqual(aluguel.VeiculoId, (await ApiCliente.JsonAsync(referencias)).GetProperty("items")[0].GetProperty("id").GetGuid());
        using var ocultacao = await EnviarAsync(cliente, HttpMethod.Delete, caminho, null, consulta.Headers.ETag!.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, ocultacao.StatusCode);
        using var invisivel = await cliente.GetAsync(caminho);
        Assert.AreEqual(HttpStatusCode.NotFound, invisivel.StatusCode);
        using var oculto = await cliente.GetAsync(caminho + "?incluirOcultos=true");
        var historico = await ApiCliente.JsonAsync(oculto);
        Assert.IsTrue(historico.GetProperty("oculto").GetBoolean());
        Assert.AreEqual(f.GetRawText(), historico.GetProperty("devolucao").GetRawText());
        using var refsPadrao = await cliente.GetAsync("/api/v1/alugueis/referencias-veiculos");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(refsPadrao)).GetProperty("total").GetInt32());
        using var refsOcultas = await cliente.GetAsync("/api/v1/alugueis/referencias-veiculos?incluirOcultos=true");
        Assert.AreEqual(1, (await ApiCliente.JsonAsync(refsOcultas)).GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task TransporteRejeitaCamposAusentesDesconhecidosNumerosOffsetEscalaEOverflow()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(c);
        var dados = await PrepararDadosAsync(c);
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(c, _fabrica);
        var caminho = $"/api/v1/alugueis/{aluguel.Id}/devolucao/previa";
        var comando = PreviaDevolucaoSqlTests.Comando(dados);
        var valido = Entrada(comando);
        var invalidos = new List<JsonObject>();
        foreach (var campo in new[] { "retornoReal", "hodometroFinal", "litrosRestantes", "taxasAdicionaisIds" })
        {
            var semCampo = (JsonObject)valido.DeepClone();
            semCampo.Remove(campo);
            invalidos.Add(semCampo);
            var nulo = (JsonObject)valido.DeepClone();
            nulo[campo] = null;
            invalidos.Add(nulo);
        }
        foreach (var (campo, valor) in new (string, JsonNode?)[]
        {
            ("empresaId", JsonValue.Create(Guid.NewGuid())),
            ("totalFinal", JsonValue.Create("0.00")),
            ("seguros", new JsonArray()),
            ("retornoReal", JsonValue.Create("2026-11-26T10:00:00")),
            ("retornoReal", JsonValue.Create("2026-02-30T10:00:00Z")),
            ("litrosRestantes", JsonValue.Create(40m)),
            ("litrosRestantes", JsonValue.Create("40,5")),
            ("litrosRestantes", JsonValue.Create("4e1")),
            ("litrosRestantes", JsonValue.Create("40.0001")),
            ("litrosRestantes", JsonValue.Create("40.00000000000000000000000000001")),
            ("litrosRestantes", JsonValue.Create("99999999999999999999999999999999999")),
            ("hodometroFinal", JsonValue.Create(1000.5m)),
            ("hodometroFinal", JsonValue.Create(2147483648L)),
        })
        {
            var entrada = (JsonObject)valido.DeepClone();
            entrada[campo] = valor;
            invalidos.Add(entrada);
        }
        foreach (var entrada in invalidos)
        {
            using var resposta = await EnviarAsync(cliente, HttpMethod.Post, caminho, entrada, VersaoCadastro.Etag(aluguel.RowVersion));
            Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode, entrada.ToJsonString());
        }
        using var utc = await EnviarAsync(cliente, HttpMethod.Post, caminho, valido, VersaoCadastro.Etag(aluguel.RowVersion));
        var offset = Entrada(comando);
        offset["retornoReal"] = comando.RetornoReal.ToOffset(TimeSpan.FromHours(-3)).ToString("O");
        using var sp = await EnviarAsync(cliente, HttpMethod.Post, caminho, offset, VersaoCadastro.Etag(aluguel.RowVersion));
        Assert.AreEqual(HttpStatusCode.OK, sp.StatusCode);
        Assert.AreEqual((await ApiCliente.JsonAsync(utc)).GetRawText(), (await ApiCliente.JsonAsync(sp)).GetRawText());
        Assert.AreEqual(0, await c.Banco.DemonstrativosFinais.CountAsync());
    }

    [TestMethod]
    public async Task CriacaoHttpReproduzResultadoOriginalMesmoDepoisDeConcluir()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(c, _fabrica);
        var comando = EstimativasSelecoesTests.Comando(c);
        var criacao = JsonSerializer.SerializeToNode(new
        {
            clienteId = comando.ClienteId, condutorId = comando.CondutorId, veiculoId = comando.VeiculoId,
            planoId = comando.PlanoId, retornoPrevisto = comando.RetornoPrevisto.ToString("O"),
            hodometroInicial = comando.HodometroInicial, taxasServicoIds = comando.TaxasServicoIds,
            seguros = comando.Seguros.Select(s => s.ToString()).ToArray(),
        })!.AsObject();
        var chave = Guid.NewGuid().ToString();
        using var criado = await EnviarAsync(cliente, HttpMethod.Post, "/api/v1/alugueis", criacao, chave: chave);
        Assert.AreEqual(HttpStatusCode.Created, criado.StatusCode);
        var original = await ApiCliente.JsonAsync(criado);
        var id = original.GetProperty("id").GetGuid();
        var dados = await PrepararDadosAsync(c);
        var entrada = Entrada(PreviaDevolucaoSqlTests.Comando(dados));
        var caminho = $"/api/v1/alugueis/{id}/devolucao";
        using var previa = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/previa", entrada, criado.Headers.ETag!.ToString());
        Assert.AreEqual(HttpStatusCode.OK, previa.StatusCode);
        entrada["versoesAceitas"] = JsonNode.Parse((await ApiCliente.JsonAsync(previa)).GetProperty("versoesAceitas").GetRawText());
        using var final = await EnviarAsync(cliente, HttpMethod.Post, caminho, entrada, criado.Headers.ETag!.ToString(), Guid.NewGuid().ToString());
        Assert.AreEqual(HttpStatusCode.OK, final.StatusCode);
        using var replay = await EnviarAsync(cliente, HttpMethod.Post, "/api/v1/alugueis", criacao, chave: chave);
        Assert.AreEqual(HttpStatusCode.Created, replay.StatusCode);
        Assert.AreEqual(original.GetRawText(), (await ApiCliente.JsonAsync(replay)).GetRawText());
        Assert.AreEqual(criado.Headers.ETag, replay.Headers.ETag);
    }

    [TestMethod]
    public async Task OpenApiPublicaRotasTiposParametrosEStatusDaF4()
    {
        using var cliente = Clientes.Criar(_fabrica);
        using var resposta = await cliente.GetAsync("/openapi/v1.json");
        var json = await ApiCliente.JsonAsync(resposta);
        var paths = json.GetProperty("paths");
        foreach (var sufixo in new[] { "/devolucao/previa", "/devolucao" })
        {
            var post = paths.GetProperty("/api/v1/alugueis/{id}" + sufixo).GetProperty("post");
            Assert.IsTrue(post.GetProperty("requestBody").GetProperty("required").GetBoolean());
            Assert.IsTrue(post.GetProperty("parameters").EnumerateArray().Any(p => p.GetProperty("name").GetString() == "If-Match"));
            foreach (var status in new[] { "200", "400", "401", "403", "404", "409", "412", "428", "503" })
                Assert.IsTrue(post.GetProperty("responses").TryGetProperty(status, out _), status);
            Assert.IsTrue(post.GetProperty("responses").GetProperty("200").TryGetProperty("content", out _));
        }
        Assert.IsTrue(paths.GetProperty("/api/v1/alugueis/{id}/devolucao").GetProperty("post")
            .GetProperty("parameters").EnumerateArray().Any(p => p.GetProperty("name").GetString() == "Idempotency-Key"));
        Assert.IsTrue(paths.GetProperty("/api/v1/alugueis/{id}").GetProperty("delete").GetProperty("responses").TryGetProperty("204", out _));
        foreach (var rota in new[] { "/api/v1/alugueis", "/api/v1/alugueis/{id}", "/api/v1/alugueis/referencias-veiculos" })
            Assert.IsTrue(paths.GetProperty(rota).GetProperty("get").GetProperty("parameters")
                .EnumerateArray().Any(p => p.GetProperty("name").GetString() == "incluirOcultos"));
        var schemas = json.GetProperty("components").GetProperty("schemas");
        Assert.IsTrue(schemas.GetProperty("EntradaDevolucao").GetProperty("properties").GetProperty("litrosRestantes")
            .GetProperty("type").EnumerateArray().Any(t => t.GetString() == "string"));
    }

    internal static JsonObject Entrada(ComandoDevolucao c) => new()
    {
        ["retornoReal"] = c.RetornoReal.ToUniversalTime().ToString("O"),
        ["hodometroFinal"] = c.HodometroFinal,
        ["litrosRestantes"] = c.LitrosRestantes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["taxasAdicionaisIds"] = JsonSerializer.SerializeToNode(c.TaxasAdicionaisIds),
    };

    [TestMethod]
    public async Task PrecondicoesChaveEAceiteRejeitamSemEfeitosComProblemasSanitizados()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(c);
        var dados = await PrepararDadosAsync(c);
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(c, _fabrica);
        var caminho = $"/api/v1/alugueis/{aluguel.Id}";
        var etag = VersaoCadastro.Etag(aluguel.RowVersion);
        var entrada = Entrada(PreviaDevolucaoSqlTests.Comando(dados));
        foreach (var versao in new[] { null, VersaoCadastro.Etag(new byte[8]), "invalida" })
        {
            using var previaInvalida = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao/previa", entrada, versao);
            await ProblemaAsync(previaInvalida, versao is null ? 428 : 412,
                versao is null ? "versao_ausente" : "versao_divergente", c);
            using var ocultacaoInvalida = await EnviarAsync(cliente, HttpMethod.Delete, caminho, null, versao);
            await ProblemaAsync(ocultacaoInvalida, versao is null ? 428 : 412,
                versao is null ? "versao_ausente" : "versao_divergente", c);
        }
        using var aberto = await EnviarAsync(cliente, HttpMethod.Delete, caminho, null, etag);
        await ProblemaAsync(aberto, 409, "aluguel_nao_ocultavel", c);
        using var previa = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao/previa", entrada, etag);
        Assert.IsTrue(ApiCliente.NoStore(previa));
        entrada["versoesAceitas"] = JsonNode.Parse((await ApiCliente.JsonAsync(previa)).GetProperty("versoesAceitas").GetRawText());
        foreach (var chave in new[] { null, "nao-guid", Guid.Empty.ToString(), Guid.NewGuid() + "," + Guid.NewGuid() })
        {
            using var invalida = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao", entrada, etag, chave);
            await ProblemaAsync(invalida, 400, "cadastro_invalido", c);
        }
        foreach (var versao in new[] { null, VersaoCadastro.Etag(new byte[8]) })
        {
            using var invalida = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao", entrada, versao, Guid.NewGuid().ToString());
            await ProblemaAsync(invalida, versao is null ? 428 : 412,
                versao is null ? "versao_ausente" : "versao_divergente", c);
        }
        foreach (var campo in new[] { "revisaoId", "combustivelId", "versaoRegra", "taxasNovas", "resumoCalculo" })
        {
            var ausente = (JsonObject)entrada.DeepClone();
            ausente["versoesAceitas"]!.AsObject().Remove(campo);
            using var invalida = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao", ausente, etag, Guid.NewGuid().ToString());
            Assert.AreEqual(HttpStatusCode.BadRequest, invalida.StatusCode);
            Assert.IsTrue(ApiCliente.NoStore(invalida));
        }
        foreach (var local in new[] { "raiz", "aceite", "taxa" })
        {
            var extra = (JsonObject)entrada.DeepClone();
            var alvo = local switch
            {
                "raiz" => extra,
                "aceite" => extra["versoesAceitas"]!.AsObject(),
                _ => extra["versoesAceitas"]!["taxasNovas"]![0]!.AsObject(),
            };
            alvo["totalFinal"] = "0.00";
            using var invalida = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao", extra, etag, Guid.NewGuid().ToString());
            await ProblemaAsync(invalida, 400, "requisicao_invalida", c);
        }
        var adulterado = (JsonObject)entrada.DeepClone();
        adulterado["versoesAceitas"]!["resumoCalculo"] = new string('A', 64);
        using var conflito = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao", adulterado, etag, Guid.NewGuid().ToString());
        await ProblemaAsync(conflito, 409, "previa_desatualizada", c);
        Assert.AreEqual(0, await c.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(0, await c.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task ReplayHttpAposLogoutLoginOcultacaoENovaOcupacaoPreservaResultadoEIntencao()
    {
        // JWT é validado pelo relógio real do bearer; este cenário de login não usa o relógio fixo da contratação.
        await using var fabricaTempoReal = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave);
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(c);
        var dados = await PrepararDadosAsync(c);
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(c, fabricaTempoReal);
        var caminho = $"/api/v1/alugueis/{aluguel.Id}";
        var etag = VersaoCadastro.Etag(aluguel.RowVersion);
        var entrada = Entrada(PreviaDevolucaoSqlTests.Comando(dados));
        using var previa = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao/previa", entrada, etag);
        entrada["versoesAceitas"] = JsonNode.Parse((await ApiCliente.JsonAsync(previa)).GetProperty("versoesAceitas").GetRawText());
        var chave = Guid.NewGuid().ToString();
        using var original = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao", entrada, etag, chave);
        Assert.AreEqual(HttpStatusCode.OK, original.StatusCode);
        Assert.IsTrue(ApiCliente.NoStore(original));
        var final = await ApiCliente.JsonAsync(original);
        using var ocultar = await EnviarAsync(cliente, HttpMethod.Delete, caminho, null, original.Headers.ETag!.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, ocultar.StatusCode);
        Assert.IsTrue(ApiCliente.NoStore(ocultar));
        using var obsoleta = await EnviarAsync(cliente, HttpMethod.Delete, caminho, null, original.Headers.ETag!.ToString());
        await ProblemaAsync(obsoleta, 412, "versao_divergente", c);
        using var consulta = await cliente.GetAsync(caminho + "?incluirOcultos=true");
        var versaoOculta = consulta.Headers.ETag!.ToString();
        using var repetir = await EnviarAsync(cliente, HttpMethod.Delete, caminho, null, versaoOculta);
        Assert.AreEqual(HttpStatusCode.NoContent, repetir.StatusCode);
        var novo = await ContratacaoSqlTests.Casos(c.Banco, c.Contexto).CriarAsync(
            EstimativasSelecoesTests.Comando(c), Guid.NewGuid(), CancellationToken.None);
        using var logout = await cliente.PostAsync("/api/v1/session/logout", null);
        Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);
        using var revogada = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao", entrada, etag, chave);
        await ProblemaAsync(revogada, 401, "sessao_invalida", c);

        // Credencial apenas sintética no catálogo isolado; exercita login HTTP real do mesmo ator.
        const string senhaSintetica = "Ficticia-Http-2026!x";
        string usuario;
        await using (var escopo = fabricaTempoReal.Services.CreateAsyncScope())
        {
            var contexto = escopo.ServiceProvider.GetRequiredService<Locadora.Infrastructure.Modulos.Transversal.ContextoEmpresa>();
            contexto.EmpresaId = c.Contexto.EmpresaId;
            contexto.ContaId = c.Contexto.ContaId;
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<UsuarioConta>>();
            var conta = (await usuarios.FindByIdAsync(c.Contexto.ContaId!.Value.ToString()))!;
            usuario = conta.UserName!;
            var adicionado = await usuarios.AddPasswordAsync(conta, senhaSintetica);
            Assert.IsTrue(adicionado.Succeeded);
        }
        using var reautenticado = Clientes.Criar(fabricaTempoReal);
        using var login = await ApiCliente.EntrarAsync(reautenticado, usuario, senhaSintetica);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var sessao = await ApiCliente.JsonAsync(login);
        Assert.AreEqual(c.Contexto.ContaId!.Value, sessao.GetProperty("contaId").GetGuid());
        ApiCliente.Bearer(reautenticado, sessao.GetProperty("accessToken").GetString()!);
        using var replay = await EnviarAsync(reautenticado, HttpMethod.Post, caminho + "/devolucao", entrada, etag, chave);
        Assert.AreEqual(HttpStatusCode.OK, replay.StatusCode);
        Assert.AreEqual(original.Headers.ETag, replay.Headers.ETag);
        Assert.AreEqual(final.GetRawText(), (await ApiCliente.JsonAsync(replay)).GetRawText());
        Assert.IsTrue(ApiCliente.NoStore(replay));
        foreach (var mudar in new[] { "entrada", "aceite", "etag", "recurso" })
        {
            var outro = (JsonObject)entrada.DeepClone();
            if (mudar == "entrada") outro["litrosRestantes"] = "39";
            if (mudar == "aceite") outro["versoesAceitas"]!["resumoCalculo"] = new string('B', 64);
            using var conflito = await EnviarAsync(reautenticado, HttpMethod.Post,
                mudar == "recurso" ? $"/api/v1/alugueis/{novo.AluguelId}/devolucao" : caminho + "/devolucao",
                outro, mudar == "etag" ? versaoOculta : etag, chave);
            await ProblemaAsync(conflito, 409, "chave_idempotente_reutilizada", c);
        }
        using var novaIntencao = await EnviarAsync(reautenticado, HttpMethod.Post, caminho + "/devolucao",
            entrada, versaoOculta, Guid.NewGuid().ToString());
        await ProblemaAsync(novaIntencao, 409, "aluguel_nao_devolvivel", c);
        c.Banco.ChangeTracker.Clear();
        Assert.AreEqual(1, await c.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(1, await c.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Devolucao));
        Assert.AreEqual(1, await c.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Ocultacao));
        Assert.AreEqual(1, await c.Banco.Alugueis.CountAsync(a => a.Estado == Domain.Modulos.Locacoes.EstadoAluguel.EmAberto));
    }

    [TestMethod]
    public async Task EndpointsNegamSemSessaoPoliticaEReferenciasDeOutraEmpresaInclusiveOcultos()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(a);
        var dados = await PrepararDadosAsync(a);
        var etag = VersaoCadastro.Etag(aluguel.RowVersion);
        var entrada = Entrada(PreviaDevolucaoSqlTests.Comando(dados));
        using var dono = await EstimativasSelecoesTests.AutenticarAsync(a, _fabrica);
        var caminho = $"/api/v1/alugueis/{aluguel.Id}";
        using var previa = await EnviarAsync(dono, HttpMethod.Post, caminho + "/devolucao/previa", entrada, etag);
        entrada["versoesAceitas"] = JsonNode.Parse((await ApiCliente.JsonAsync(previa)).GetProperty("versoesAceitas").GetRawText());
        using var externo = await EstimativasSelecoesTests.AutenticarAsync(b, _fabrica);
        using var anonimo = Clientes.Criar(_fabrica);
        await using var politica = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave,
            servicos: s => s.AddAuthorization(o => o.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().RequireClaim("permissao-sintetica-ausente").Build()));
        using var proibido = await EstimativasSelecoesTests.AutenticarAsync(a, politica);
        foreach (var (metodo, rota, corpo) in new (HttpMethod, string, JsonObject?)[]
        {
            (HttpMethod.Get, caminho + "?incluirOcultos=true", null),
            (HttpMethod.Get, "/api/v1/alugueis/referencias-veiculos?incluirOcultos=true", null),
            (HttpMethod.Post, caminho + "/devolucao/previa", Entrada(PreviaDevolucaoSqlTests.Comando(dados))),
            (HttpMethod.Post, caminho + "/devolucao", entrada),
            (HttpMethod.Delete, caminho, null),
        })
        {
            using var semSessao = await EnviarAsync(anonimo, metodo, rota, corpo, etag, Guid.NewGuid().ToString());
            await ProblemaAsync(semSessao, 401, "sessao_invalida", a);
            using var negado = await EnviarAsync(proibido, metodo, rota, corpo, etag, Guid.NewGuid().ToString());
            await ProblemaAsync(negado, 403, "acesso_negado", a);
            Assert.IsFalse((await negado.Content.ReadAsStringAsync()).Contains(a.Dados.Cliente.Cpf!));
            using var cruzado = await EnviarAsync(externo, metodo, rota, corpo, etag, Guid.NewGuid().ToString());
            if (rota.Contains("referencias-veiculos"))
            {
                Assert.AreEqual(0, (await ApiCliente.JsonAsync(cruzado)).GetProperty("total").GetInt32());
            }
            else await ProblemaAsync(cruzado, 404, "cadastro_nao_encontrado", a);
        }
        var taxaExterna = Entrada(PreviaDevolucaoSqlTests.Comando(dados) with { TaxasAdicionaisIds = [b.Dados.Taxas.Single().Id] });
        using var referencia = await EnviarAsync(dono, HttpMethod.Post, caminho + "/devolucao/previa", taxaExterna, etag);
        await ProblemaAsync(referencia, 404, "cadastro_nao_encontrado", a);
        var chaveOriginal = Guid.NewGuid().ToString();
        using var final = await EnviarAsync(dono, HttpMethod.Post, caminho + "/devolucao", entrada, etag, chaveOriginal);
        Assert.AreEqual(HttpStatusCode.OK, final.StatusCode);
        using var ocultar = await EnviarAsync(dono, HttpMethod.Delete, caminho, null, final.Headers.ETag!.ToString());
        Assert.AreEqual(HttpStatusCode.NoContent, ocultar.StatusCode);
        using var historicoExterno = await externo.GetAsync(caminho + "?incluirOcultos=true");
        await ProblemaAsync(historicoExterno, 404, "cadastro_nao_encontrado", a);
        using var replayExterno = await EnviarAsync(externo, HttpMethod.Post, caminho + "/devolucao", entrada, etag, chaveOriginal);
        await ProblemaAsync(replayExterno, 404, "cadastro_nao_encontrado", a);
        using var refsExternas = await externo.GetAsync("/api/v1/alugueis/referencias-veiculos?incluirOcultos=true");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(refsExternas)).GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task FaltaVigenciaMudancaRelevanteEIndisponibilidadeRetornamErrosSemFechamento()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(c);
        var etag = VersaoCadastro.Etag(aluguel.RowVersion);
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(c, _fabrica);
        var caminho = $"/api/v1/alugueis/{aluguel.Id}/devolucao";
        var entrada = Entrada(new(CenarioLocacoesSql.Agora.AddDays(2), 1000, 50, []));
        using var semPreco = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/previa", entrada, etag);
        await ProblemaAsync(semPreco, 409, "combustivel_sem_vigencia", c);
        var dados = await PrepararDadosAsync(c);
        entrada = Entrada(PreviaDevolucaoSqlTests.Comando(dados));
        using var previa = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/previa", entrada, etag);
        entrada["versoesAceitas"] = JsonNode.Parse((await ApiCliente.JsonAsync(previa)).GetProperty("versoesAceitas").GetRawText());
        await Unidade(c).ExecutarAsync(async token =>
        {
            var taxa = await c.Banco.TaxasServicos.SingleAsync(t => t.Id == dados.TaxasAdicionais.Single().Id, token);
            taxa.Alterar("Taxa sintética reajustada", 20m, taxa.Modalidade, CenarioLocacoesSql.Agora);
            return 0;
        }, CancellationToken.None);
        using var mudou = await EnviarAsync(cliente, HttpMethod.Post, caminho, entrada, etag, Guid.NewGuid().ToString());
        await ProblemaAsync(mudou, 409, "previa_desatualizada", c);
        await using var fabricaIndisponivel = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave, servicos: s =>
        {
            s.RemoveAll<IOperacoesDevolucao>();
            s.AddScoped<IOperacoesDevolucao, DevolucaoIndisponivel>();
        });
        using var indisponivel = await EstimativasSelecoesTests.AutenticarAsync(c, fabricaIndisponivel);
        using var falha = await EnviarAsync(indisponivel, HttpMethod.Post, caminho, entrada, etag, Guid.NewGuid().ToString());
        await ProblemaAsync(falha, 503, "dependencia_indisponivel", c);
        Assert.AreEqual(0, await c.Banco.DemonstrativosFinais.CountAsync());
        Assert.AreEqual(0, await c.Banco.AuditoriasCadastro.CountAsync());
    }

    private sealed class DevolucaoIndisponivel : IOperacoesDevolucao
    {
        public Task<ResultadoPreviaDevolucao> PreviaAsync(Guid id, ComandoDevolucao comando, string? versao, CancellationToken cancelamento)
            => throw new ArmazenamentoIndisponivelException();
        public Task<ResultadoDevolucao> ConcluirAsync(Guid id, ComandoConclusaoDevolucao comando, string? versao, Guid chave, CancellationToken cancelamento)
            => throw new ArmazenamentoIndisponivelException();
    }

    private static async Task ProblemaAsync(HttpResponseMessage resposta, int status, string codigo, CenarioLocacoesSql c)
    {
        Assert.AreEqual(status, (int)resposta.StatusCode);
        Assert.IsTrue(ApiCliente.NoStore(resposta));
        Assert.AreEqual("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var json = await ApiCliente.JsonAsync(resposta);
        Assert.AreEqual(status, json.GetProperty("status").GetInt32());
        Assert.AreEqual(codigo, json.GetProperty("code").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
        var texto = json.GetRawText();
        foreach (var proibido in new[] { c.Dados.Cliente.Cpf!, c.Dados.Cliente.Cnh!, "SELECT ", "SqlException", "Bearer ", "Password", "ConnectionStrings" })
            Assert.IsFalse(texto.Contains(proibido, StringComparison.OrdinalIgnoreCase), proibido);
    }

    internal static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, HttpMethod metodo,
        string caminho, JsonObject? entrada, string? etag = null, string? chave = null)
    {
        using var pedido = new HttpRequestMessage(metodo, caminho);
        if (etag is not null) pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        if (chave is not null) pedido.Headers.TryAddWithoutValidation("Idempotency-Key", chave);
        if (entrada is not null) pedido.Content = JsonContent.Create(entrada);
        return await cliente.SendAsync(pedido);
    }
}
