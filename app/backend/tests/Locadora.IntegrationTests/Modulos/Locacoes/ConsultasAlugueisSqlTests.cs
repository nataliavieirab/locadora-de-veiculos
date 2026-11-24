using System.Net;
using System.Net.Http.Json;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class ConsultasAlugueisSqlTests
{
    [TestMethod]
    public async Task ListaVaziaExigeAutenticacaoERetornaEnvelope()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario);
        using var lista = await cliente.GetAsync("/api/v1/alugueis");
        Assert.AreEqual(HttpStatusCode.OK, lista.StatusCode);
        Assert.IsTrue(ApiCliente.NoStore(lista));
        var json = await ApiCliente.JsonAsync(lista);
        Assert.AreEqual(0, json.GetProperty("total").GetInt32());
        Assert.AreEqual(0, json.GetProperty("items").GetArrayLength());
        using var semSessao = Clientes.Criar();
        using var negado = await semSessao.GetAsync("/api/v1/alugueis");
        Assert.AreEqual(HttpStatusCode.Unauthorized, negado.StatusCode);
    }

    [TestMethod]
    public async Task ListaProjetCamposMascaraCpfOrdenaEPagina()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var tempo = new ContratacaoSqlTests.Relogio();
        var casos = ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto, tempo);
        var primeiro = await casos.CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var veiculoB = Veiculo.Novo(cenario.Contexto.EmpresaId!.Value, new("FIC2B01", "Marca", "Verde", "Modelo B",
            TipoCombustivel.Gasolina, 40, 2025, cenario.Dados.Grupo.Id), CenarioLocacoesSql.Agora.AddMinutes(5));
        cenario.Banco.Add(veiculoB);
        await cenario.Banco.SaveChangesAsync();
        tempo.Agora = tempo.Agora.AddHours(1);
        var segundo = await casos.CriarAsync(comando with { VeiculoId = veiculoB.Id }, Guid.NewGuid(), CancellationToken.None);
        cenario.Banco.ChangeTracker.Clear();
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario);
        using var resposta = await cliente.GetAsync("/api/v1/alugueis?pageSize=1");
        var pagina1 = await ApiCliente.JsonAsync(resposta);
        Assert.AreEqual(2, pagina1.GetProperty("total").GetInt32());
        Assert.AreEqual(1, pagina1.GetProperty("items").GetArrayLength());
        var item = pagina1.GetProperty("items")[0];
        Assert.AreEqual(segundo.AluguelId, item.GetProperty("id").GetGuid());
        Assert.AreEqual("EmAberto", item.GetProperty("estado").GetString());
        Assert.AreEqual("***.***.***-32", item.GetProperty("condutorCpfResumo").GetString());
        Assert.AreEqual("254.00", item.GetProperty("totalConhecido").GetString());
        Assert.AreEqual(1, item.GetProperty("revisao").GetInt32());
        using var pagina2 = await cliente.GetAsync("/api/v1/alugueis?page=2&pageSize=1");
        Assert.AreEqual(primeiro.AluguelId, (await ApiCliente.JsonAsync(pagina2)).GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [TestMethod]
    public async Task FiltrosVeiculoPeriodoExclusiveEstadoEPeriodoInvalido()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(
            EstimativasSelecoesTests.Comando(cenario), Guid.NewGuid(), CancellationToken.None);
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario);
        var saida = HorarioSaoPaulo.ParaTransporte(CenarioLocacoesSql.Agora);
        var limite = HorarioSaoPaulo.ParaTransporte(CenarioLocacoesSql.Agora.AddMinutes(1));
        using var incluso = await cliente.GetAsync($"/api/v1/alugueis?saidaDe={Uri.EscapeDataString(saida)}&saidaAte={Uri.EscapeDataString(limite)}");
        Assert.AreEqual(1, (await ApiCliente.JsonAsync(incluso)).GetProperty("total").GetInt32());
        var fora = HorarioSaoPaulo.ParaTransporte(CenarioLocacoesSql.Agora.AddDays(10));
        var foraLimite = HorarioSaoPaulo.ParaTransporte(CenarioLocacoesSql.Agora.AddDays(11));
        using var exclusivo = await cliente.GetAsync(
            $"/api/v1/alugueis?saidaDe={Uri.EscapeDataString(fora)}&saidaAte={Uri.EscapeDataString(foraLimite)}");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(exclusivo)).GetProperty("total").GetInt32());
        using var veiculo = await cliente.GetAsync($"/api/v1/alugueis?veiculoId={cenario.Dados.Veiculo.Id}");
        Assert.AreEqual(1, (await ApiCliente.JsonAsync(veiculo)).GetProperty("total").GetInt32());
        using var estado = await cliente.GetAsync("/api/v1/alugueis?estado=EmAberto");
        Assert.AreEqual(1, (await ApiCliente.JsonAsync(estado)).GetProperty("total").GetInt32());
        using var periodoInvalido = await cliente.GetAsync($"/api/v1/alugueis?saidaDe={Uri.EscapeDataString(limite)}&saidaAte={Uri.EscapeDataString(saida)}");
        await ProblemaAsync(periodoInvalido, 400, "cadastro_invalido");
        using var estadoInvalido = await cliente.GetAsync("/api/v1/alugueis?estado=Cancelado");
        await ProblemaAsync(estadoInvalido, 400, "cadastro_invalido");
        await using var outra = await CenarioLocacoesSql.CriarAsync();
        using var filtroExterno = await cliente.GetAsync($"/api/v1/alugueis?veiculoId={outra.Dados.Veiculo.Id}");
        Assert.AreEqual(0, (await ApiCliente.JsonAsync(filtroExterno)).GetProperty("total").GetInt32());
        Assert.AreEqual(criado.AluguelId, (await ApiCliente.JsonAsync(await cliente.GetAsync($"/api/v1/alugueis/{criado.AluguelId}")))
            .GetProperty("id").GetGuid());
    }

    [TestMethod]
    public async Task DetalhePreservaSnapshotsEstrangeiroInexistenteSemVazamento()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var nomeOriginal = (await cenario.Banco.Clientes.SingleAsync()).Nome;
        var plano = await cenario.Banco.Planos.SingleAsync();
        plano.Alterar(900, null, 100, 5, CenarioLocacoesSql.Agora.AddMinutes(1));
        var clienteCadastro = await cenario.Banco.Clientes.SingleAsync();
        clienteCadastro.Alterar(new(TipoCliente.PF, "Nome alterado", cenario.Dados.Cliente.Cpf!, null,
            cenario.Dados.Cliente.Rg!, cenario.Dados.Cliente.Cnh!, cenario.Dados.Cliente.Telefone,
            cenario.Dados.Cliente.Endereco, cenario.Dados.Cliente.Email), CenarioLocacoesSql.Agora.AddMinutes(1));
        await cenario.Banco.SaveChangesAsync();
        cenario.Banco.ChangeTracker.Clear();
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(cenario);
        using var detalhe = await cliente.GetAsync($"/api/v1/alugueis/{criado.AluguelId}");
        Assert.AreEqual(HttpStatusCode.OK, detalhe.StatusCode);
        Assert.IsNotNull(detalhe.Headers.ETag);
        var json = await ApiCliente.JsonAsync(detalhe);
        Assert.AreEqual(nomeOriginal, json.GetProperty("cliente").GetProperty("nome").GetString());
        Assert.AreEqual("100.00", json.GetProperty("plano").GetProperty("precoDiario").GetString());
        Assert.AreEqual("254.00", json.GetProperty("estimativa").GetProperty("totalConhecido").GetString());
        Assert.AreEqual(cenario.Dados.Cliente.Id, json.GetProperty("clienteId").GetGuid());
        await using var outra = await CenarioLocacoesSql.CriarAsync();
        var estrangeiro = await ContratacaoSqlTests.Casos(outra.Banco, outra.Contexto).CriarAsync(
            EstimativasSelecoesTests.Comando(outra), Guid.NewGuid(), CancellationToken.None);
        using var inexistente = await cliente.GetAsync($"/api/v1/alugueis/{estrangeiro.AluguelId}");
        await ProblemaAsync(inexistente, 404, "cadastro_nao_encontrado");
        using var ausente = await cliente.GetAsync($"/api/v1/alugueis/{Guid.NewGuid()}");
        await ProblemaAsync(ausente, 404, "cadastro_nao_encontrado");
    }

    private static async Task ProblemaAsync(HttpResponseMessage resposta, int status, string codigo)
    {
        Assert.AreEqual(status, (int)resposta.StatusCode);
        var json = await ApiCliente.JsonAsync(resposta);
        Assert.AreEqual(codigo, json.GetProperty("code").GetString());
    }
}
