using System.Net;
using System.Net.Http.Json;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class AlugueisHttpTests
{
    private FabricaApi _fabrica = null!;

    [TestInitialize]
    public void Iniciar() => _fabrica = new(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave, servicos: servicos =>
    {
        servicos.RemoveAll<TimeProvider>();
        servicos.AddSingleton<TimeProvider>(new RelogioContratacao());
    });

    [TestCleanup]
    public async Task Encerrar() => await _fabrica.DisposeAsync();

    private sealed class RelogioContratacao : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => CenarioLocacoesSql.Agora;
    }

    [TestMethod]
    public async Task PostCriaComIdempotencyKeyRetornaLocationEtagEReplay()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var chave = Guid.NewGuid();
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario, _fabrica);
        var antes = await cenario.Banco.Alugueis.CountAsync();
        using var criado = await PostContratacaoAsync(cliente, chave, Entrada(cenario, comando));
        Assert.AreEqual(HttpStatusCode.Created, criado.StatusCode);
        Assert.IsTrue(ApiCliente.NoStore(criado));
        Assert.AreEqual($"/api/v1/alugueis/{(await ApiCliente.JsonAsync(criado)).GetProperty("id").GetGuid()}",
            criado.Headers.Location?.ToString());
        Assert.IsNotNull(criado.Headers.ETag);
        Assert.AreEqual(antes + 1, await cenario.Banco.Alugueis.CountAsync());
        using var replay = await PostContratacaoAsync(cliente, chave, Entrada(cenario, comando));
        Assert.AreEqual(HttpStatusCode.Created, replay.StatusCode);
        var idOriginal = (await ApiCliente.JsonAsync(criado)).GetProperty("id").GetGuid();
        Assert.AreEqual(idOriginal, (await ApiCliente.JsonAsync(replay)).GetProperty("id").GetGuid());
        Assert.AreEqual(antes + 1, await cenario.Banco.Alugueis.CountAsync());
        using var semChave = await cliente.PostAsJsonAsync("/api/v1/alugueis", Entrada(cenario, comando));
        await ProblemaAsync(semChave, 400, "cadastro_invalido");
        using var corpoDiferente = await PostContratacaoAsync(cliente, chave, Entrada(cenario, comando with { HodometroInicial = 77 }));
        await ProblemaAsync(corpoDiferente, 409, "chave_idempotente_reutilizada");
        using var semSessao = Clientes.Criar();
        using var negado = await PostContratacaoAsync(semSessao, Guid.NewGuid(), Entrada(cenario, comando));
        Assert.AreEqual(HttpStatusCode.Unauthorized, negado.StatusCode);
    }

    [TestMethod]
    public async Task PostEstimativaNaoPersisteEEdicaoExigeIfMatch()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario, _fabrica);
        var alugueis = await cenario.Banco.Alugueis.CountAsync();
        var auditorias = await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Entidade == Domain.Modulos.Transversal.TipoCadastro.Aluguel);
        using var previa = await cliente.PostAsJsonAsync("/api/v1/alugueis/estimativas", Entrada(cenario, comando));
        Assert.AreEqual(HttpStatusCode.OK, previa.StatusCode);
        Assert.IsTrue(ApiCliente.NoStore(previa));
        var json = await ApiCliente.JsonAsync(previa);
        Assert.AreEqual("254.00", json.GetProperty("estimativa").GetProperty("totalConhecido").GetString());
        Assert.AreEqual(alugueis, await cenario.Banco.Alugueis.CountAsync());
        Assert.AreEqual(auditorias, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Entidade == Domain.Modulos.Transversal.TipoCadastro.Aluguel));
        using var extra = await cliente.PostAsJsonAsync("/api/v1/alugueis/estimativas", new
        {
            clienteId = comando.ClienteId,
            condutorId = comando.CondutorId,
            veiculoId = comando.VeiculoId,
            planoId = comando.PlanoId,
            retornoPrevisto = HorarioSaoPaulo.ParaTransporte(comando.RetornoPrevisto),
            hodometroInicial = comando.HodometroInicial,
            taxasServicoIds = comando.TaxasServicoIds,
            seguros = comando.Seguros.Select(s => s.ToString()).ToArray(),
            campoInexistente = true,
        });
        Assert.AreEqual(HttpStatusCode.BadRequest, extra.StatusCode);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        using var semVersao = await cliente.PostAsJsonAsync("/api/v1/alugueis/estimativas", Entrada(cenario, comando, criado.AluguelId));
        await ProblemaAsync(semVersao, 428, "versao_ausente");
        var etag = VersaoCadastro.Etag(criado.RowVersion);
        using var estimativaEdicao = await EstimarEdicaoAsync(cliente, criado.AluguelId, etag, cenario, comando);
        Assert.AreEqual(HttpStatusCode.OK, estimativaEdicao.StatusCode);
        Assert.AreEqual(2, (await ApiCliente.JsonAsync(estimativaEdicao)).GetProperty("revisao").GetInt32());
    }

    [TestMethod]
    public async Task PutExigeIfMatchRetornaEtagEProblemasPadronizados()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var etag = VersaoCadastro.Etag(criado.RowVersion);
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario, _fabrica);
        var caminho = $"/api/v1/alugueis/{criado.AluguelId}";
        using var semVersao = await cliente.PutAsJsonAsync(caminho, Entrada(cenario, comando));
        await ProblemaAsync(semVersao, 428, "versao_ausente");
        using var ok = await MutarAsync(cliente, caminho, etag, Entrada(cenario, comando with { Seguros = [] }));
        Assert.AreEqual(HttpStatusCode.OK, ok.StatusCode);
        Assert.IsNotNull(ok.Headers.ETag);
        Assert.AreNotEqual(etag, ok.Headers.ETag!.ToString());
        Assert.IsTrue(ApiCliente.NoStore(ok));
        var corpo = await ApiCliente.JsonAsync(ok);
        Assert.AreEqual(2, corpo.GetProperty("revisao").GetInt32());
        Assert.AreEqual(0, corpo.GetProperty("itens").EnumerateArray().Count(i => i.GetProperty("categoria").GetString() == "Seguro"));
        using var obsoleta = await MutarAsync(cliente, caminho, etag, Entrada(cenario, comando));
        await ProblemaAsync(obsoleta, 412, "versao_divergente");
        using var inexistente = await MutarAsync(cliente, $"/api/v1/alugueis/{Guid.NewGuid()}", etag, Entrada(cenario, comando));
        await ProblemaAsync(inexistente, 404, "cadastro_nao_encontrado");
    }

    [TestMethod]
    public async Task PutTrocaVeiculoIndisponivelRetorna409SemAlterarContrato()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var reserva = Veiculo.Novo(cenario.Contexto.EmpresaId!.Value, new("HTP9Z99", "Marca", "Cinza", "Ocupado",
            TipoCombustivel.Gas, 45, 2026, cenario.Dados.Grupo.Id), CenarioLocacoesSql.Agora);
        cenario.Banco.Add(reserva);
        await cenario.Banco.SaveChangesAsync();
        var principal = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(
            comando with { VeiculoId = reserva.Id }, Guid.NewGuid(), CancellationToken.None);
        cenario.Banco.ChangeTracker.Clear();
        principal = principal with
        {
            RowVersion = (await cenario.Banco.Alugueis.FindAsync(principal.AluguelId))!.RowVersion.ToArray(),
        };
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario, _fabrica);
        using var conflito = await MutarAsync(cliente, $"/api/v1/alugueis/{principal.AluguelId}",
            VersaoCadastro.Etag(principal.RowVersion), Entrada(cenario, comando with { VeiculoId = reserva.Id }));
        await ProblemaAsync(conflito, 409, "veiculo_indisponivel");
        var json = await ApiCliente.JsonAsync(conflito);
        Assert.IsTrue(json.GetProperty("errors").TryGetProperty("veiculoId", out _));
    }

    [TestMethod]
    public async Task ContratoHttpRejeitaFormatoEscopoEIntervalosSemExporDocumentos()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(a, _fabrica);
        var comando = EstimativasSelecoesTests.Comando(a);
        foreach (var externo in new[]
        {
            comando with { ClienteId = b.Dados.Cliente.Id }, comando with { CondutorId = b.Dados.Condutor.Id },
            comando with { VeiculoId = b.Dados.Veiculo.Id }, comando with { PlanoId = b.Dados.Plano.Id },
            comando with { TaxasServicoIds = [b.Dados.Taxas.Single().Id] },
        })
        {
            using var negado = await cliente.PostAsJsonAsync("/api/v1/alugueis/estimativas", Entrada(a, externo));
            await ProblemaAsync(negado, 404, "cadastro_nao_encontrado");
            Assert.IsFalse((await negado.Content.ReadAsStringAsync()).Contains(a.Dados.Cliente.Cpf!));
        }
        foreach (var campo in new[] { "retornoPrevisto", "hodometroInicial", "seguros" })
        {
            var entrada = System.Text.Json.JsonSerializer.SerializeToNode(Entrada(a, comando))!.AsObject();
            entrada[campo] = campo switch
            {
                "retornoPrevisto" => System.Text.Json.Nodes.JsonValue.Create("2026-11-25T10:00:00"),
                "hodometroInicial" => System.Text.Json.Nodes.JsonValue.Create(1.5m),
                _ => System.Text.Json.JsonSerializer.SerializeToNode(new[] { "Cliente", "Cliente" }),
            };
            using var invalido = await cliente.PostAsJsonAsync("/api/v1/alugueis/estimativas", entrada);
            Assert.AreEqual(HttpStatusCode.BadRequest, invalido.StatusCode);
        }
        foreach (var filtro in new[] { "estado=Cancelado", "pageSize=101", "saidaDe=2026-11-25", "saidaDe=2026-11-25T13:00:00Z&saidaAte=2026-11-24T13:00:00Z" })
        {
            using var invalido = await cliente.GetAsync($"/api/v1/alugueis?{filtro}");
            Assert.AreEqual(HttpStatusCode.BadRequest, invalido.StatusCode);
        }
        using var openapi = await cliente.GetAsync("/openapi/v1.json");
        Assert.AreEqual(HttpStatusCode.OK, openapi.StatusCode);
        var caminhos = (await ApiCliente.JsonAsync(openapi)).GetProperty("paths");
        Assert.AreEqual(2, caminhos.GetProperty("/api/v1/alugueis").EnumerateObject().Count());
        Assert.AreEqual(3, caminhos.GetProperty("/api/v1/alugueis/{id}").EnumerateObject().Count());
        Assert.AreEqual(1, caminhos.GetProperty("/api/v1/alugueis/estimativas").EnumerateObject().Count());
        Assert.AreEqual(0, await a.Banco.Alugueis.CountAsync());
    }

    [TestMethod]
    public async Task DependenciaIndisponivelRetorna503SanitizadoNaConsulta()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        await using var fabrica = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave, servicos: servicos =>
        {
            servicos.RemoveAll<IConsultasAluguel>();
            servicos.AddScoped<IConsultasAluguel, ConsultasIndisponiveis>();
        });
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario, fabrica);
        using var resposta = await cliente.GetAsync("/api/v1/alugueis");
        await ProblemaAsync(resposta, 503, "dependencia_indisponivel");
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.IsFalse(corpo.Contains(cenario.Dados.Cliente.Cpf!));
        Assert.IsFalse(corpo.Contains("Exception"));
        Assert.IsFalse(corpo.Contains("Bearer"));
        Assert.IsTrue((await ApiCliente.JsonAsync(resposta)).TryGetProperty("traceId", out _));
    }

    [TestMethod]
    public async Task PoliticaNegadaRetorna403SemDadosContratuais()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        // Injeção só no host de teste: a v1 não cria novos perfis ou políticas.
        await using var fabrica = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave, servicos: servicos =>
            servicos.AddAuthorization(opcoes => opcoes.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().RequireClaim("permissao-sintetica-ausente").Build()));
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario, fabrica);
        using var resposta = await cliente.GetAsync("/api/v1/alugueis");
        Assert.AreEqual(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.IsFalse((await resposta.Content.ReadAsStringAsync()).Contains(cenario.Dados.Cliente.Cpf!));
    }

    private sealed class ConsultasIndisponiveis : IConsultasAluguel
    {
        public Task<Pagina<ItemListaAluguel>> ListarAsync(Paginacao paginacao, FiltroConsultaAlugueis filtro, CancellationToken cancelamento)
            => throw new ArmazenamentoIndisponivelException();
        public Task<ResultadoConsultaAluguel> ConsultarAsync(Guid id, CancellationToken cancelamento)
            => throw new ArmazenamentoIndisponivelException();
        public Task<ResultadoConsultaAluguel> ConsultarAsync(Guid id, bool incluirOcultos, CancellationToken cancelamento)
            => throw new ArmazenamentoIndisponivelException();
        public Task<Pagina<ReferenciaVeiculoHistorico>> ReferenciasVeiculosAsync(Paginacao paginacao,
            bool incluirOcultos, CancellationToken cancelamento)
            => throw new ArmazenamentoIndisponivelException();
    }

    private static object Entrada(CenarioLocacoesSql cenario, ComandoContratacao comando, Guid? aluguelId = null)
    {
        if (aluguelId is Guid id)
        {
            return new
            {
                aluguelId = id,
                clienteId = comando.ClienteId,
                condutorId = comando.CondutorId,
                veiculoId = comando.VeiculoId,
                planoId = comando.PlanoId,
                retornoPrevisto = HorarioSaoPaulo.ParaTransporte(comando.RetornoPrevisto),
                hodometroInicial = comando.HodometroInicial,
                taxasServicoIds = comando.TaxasServicoIds,
                seguros = comando.Seguros.Select(s => s.ToString()).ToArray(),
            };
        }

        return new
        {
            clienteId = comando.ClienteId,
            condutorId = comando.CondutorId,
            veiculoId = comando.VeiculoId,
            planoId = comando.PlanoId,
            retornoPrevisto = HorarioSaoPaulo.ParaTransporte(comando.RetornoPrevisto),
            hodometroInicial = comando.HodometroInicial,
            taxasServicoIds = comando.TaxasServicoIds,
            seguros = comando.Seguros.Select(s => s.ToString()).ToArray(),
        };
    }

    private static async Task<HttpResponseMessage> PostContratacaoAsync(HttpClient cliente, Guid chave, object corpo)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/v1/alugueis");
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", chave.ToString());
        pedido.Content = JsonContent.Create(corpo);
        return await cliente.SendAsync(pedido);
    }

    private static async Task<HttpResponseMessage> EstimarEdicaoAsync(HttpClient cliente, Guid aluguelId, string etag,
        CenarioLocacoesSql cenario, ComandoContratacao comando)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/v1/alugueis/estimativas");
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        pedido.Content = JsonContent.Create(Entrada(cenario, comando, aluguelId));
        return await cliente.SendAsync(pedido);
    }

    private static async Task<HttpResponseMessage> MutarAsync(HttpClient cliente, string caminho, string etag, object corpo)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Put, caminho);
        pedido.Headers.TryAddWithoutValidation("If-Match", etag);
        pedido.Content = JsonContent.Create(corpo);
        return await cliente.SendAsync(pedido);
    }

    private static async Task ProblemaAsync(HttpResponseMessage resposta, int status, string codigo)
    {
        Assert.AreEqual(status, (int)resposta.StatusCode);
        Assert.AreEqual("application/problem+json", resposta.Content.Headers.ContentType!.MediaType);
        var json = await ApiCliente.JsonAsync(resposta);
        Assert.AreEqual(status, json.GetProperty("status").GetInt32());
        Assert.AreEqual(codigo, json.GetProperty("code").GetString());
    }
}
