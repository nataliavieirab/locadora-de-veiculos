using System.Net;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class EstimativasSelecoesTests
{
    [TestMethod]
    public async Task PreviaNaoEscreveNemOcupaEEdicaoPreservaSaidaPrecosEVersao()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var banco = cenario.Banco;
        var consultas = new ConsultasContratacao(banco);
        var casos = new CasosEstimativas(consultas, cenario.Contexto, new Relogio());
        var comando = Comando(cenario);
        var previa = await casos.EstimarAsync(comando, null, null, CancellationToken.None);
        Assert.AreEqual(254m, previa.Revisao.Estimativa.TotalConhecido);
        Assert.AreEqual(0, await banco.Alugueis.CountAsync());
        Assert.AreEqual(0, await banco.RevisoesContrato.CountAsync());
        Assert.AreEqual(0, await banco.AuditoriasCadastro.CountAsync());
        Assert.IsFalse(await consultas.VeiculoOcupadoAsync(comando.VeiculoId, null, CancellationToken.None));
        cenario.Dados.Plano.Alterar(120, null, 100, 2, CenarioLocacoesSql.Agora.AddMinutes(1));
        await banco.SaveChangesAsync();
        var dadosAtuais = await consultas.CarregarAsync(comando, CancellationToken.None);
        var aluguel = Aluguel.Criar(cenario.Contexto.EmpresaId!.Value, dadosAtuais, CenarioLocacoesSql.Agora.AddMinutes(1));
        banco.Add(aluguel);
        await banco.SaveChangesAsync();
        Assert.AreEqual(294m, aluguel.RevisaoAtual.Estimativa.TotalConhecido);
        var etag = VersaoCadastro.Etag(aluguel.RowVersion);
        cenario.Dados.Plano.Alterar(180, null, 100, 2, CenarioLocacoesSql.Agora.AddMinutes(2));
        cenario.Dados.Seguros.First().Alterar(50, CenarioLocacoesSql.Agora.AddMinutes(2));
        await banco.SaveChangesAsync();
        var edicao = await casos.EstimarAsync(comando with { RetornoPrevisto = CenarioLocacoesSql.Agora.AddDays(3) },
            aluguel.Id, etag, CancellationToken.None);
        Assert.AreEqual(aluguel.SaidaUtc, edicao.SaidaUtc);
        Assert.AreEqual(426m, edicao.Revisao.Estimativa.TotalConhecido);
        Assert.AreEqual(1, await banco.RevisoesContrato.CountAsync());
        Assert.AreEqual(0, await banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(1, (await banco.Alugueis.SingleAsync()).NumeroRevisaoAtual);
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.EstimarAsync(comando, aluguel.Id, null, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.EstimarAsync(comando, aluguel.Id, VersaoCadastro.Etag(new byte[8]), CancellationToken.None));
    }

    [TestMethod]
    public async Task RejeitaReferenciaExternaPlanoIncompativelSeguroAusenteEAceitaZero()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var casos = new CasosEstimativas(new ConsultasContratacao(a.Banco), a.Contexto, new Relogio());
        var comando = Comando(a);
        foreach (var externo in new[]
        {
            comando with { ClienteId = b.Dados.Cliente.Id }, comando with { CondutorId = b.Dados.Condutor.Id },
            comando with { VeiculoId = b.Dados.Veiculo.Id }, comando with { PlanoId = b.Dados.Plano.Id },
            comando with { TaxasServicoIds = [b.Dados.Taxas.Single().Id] },
        })
            await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => casos.EstimarAsync(externo, null, null, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            casos.EstimarAsync(comando, Guid.NewGuid(), null, CancellationToken.None));
        var outroGrupo = Grupo.Novo(a.Contexto.EmpresaId!.Value, "Outro grupo", CenarioLocacoesSql.Agora);
        var outroPlano = Plano.Novo(outroGrupo.EmpresaId, outroGrupo.Id, ModalidadePlano.Livre, 10, null, null, null, CenarioLocacoesSql.Agora);
        a.Banco.AddRange(outroGrupo, outroPlano);
        await a.Banco.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            casos.EstimarAsync(comando with { PlanoId = outroPlano.Id }, null, null, CancellationToken.None));
        var seguro = a.Dados.Seguros.First();
        seguro.Alterar(0, CenarioLocacoesSql.Agora);
        await a.Banco.SaveChangesAsync();
        Assert.AreEqual(244m, (await casos.EstimarAsync(comando, null, null, CancellationToken.None)).Revisao.Estimativa.TotalConhecido);
        // Configuração ainda não contratada; ausência é simulada removendo somente no catálogo de teste.
        a.Banco.Remove(seguro);
        await a.Banco.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => casos.EstimarAsync(comando, null, null, CancellationToken.None));
        var semOpcionais = await casos.EstimarAsync(comando with { Seguros = [] }, null, null, CancellationToken.None);
        Assert.AreEqual(230m, semOpcionais.Revisao.Estimativa.TotalConhecido);
        a.Dados.Veiculo.Excluir(CenarioLocacoesSql.Agora);
        await a.Banco.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => casos.EstimarAsync(comando, null, null, CancellationToken.None));
    }

    [TestMethod]
    public async Task SelecaoHttpPaginadaPreservaCadastroEIncluiVeiculoDoProprioContrato()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var contrato = Aluguel.Criar(a.Contexto.EmpresaId!.Value, a.Dados, CenarioLocacoesSql.Agora);
        a.Banco.Add(contrato);
        for (var i = 1; i <= 21; i++)
            a.Banco.Add(Veiculo.Novo(a.Contexto.EmpresaId.Value, new($"FIC{i:D4}", "Marca", "Azul", "Modelo",
                TipoCombustivel.Gasolina, 50, 2026, a.Dados.Grupo.Id), CenarioLocacoesSql.Agora));
        await a.Banco.SaveChangesAsync();
        using var cliente = await AutenticarAsync(a);
        using var cadastro = await cliente.GetAsync("/api/v1/veiculos");
        Assert.AreEqual(22, (await ApiCliente.JsonAsync(cadastro)).GetProperty("total").GetInt32());
        using var pagina1 = await cliente.GetAsync("/api/v1/veiculos?somenteDisponiveis=true");
        using var pagina2 = await cliente.GetAsync("/api/v1/veiculos?somenteDisponiveis=true&page=2");
        var primeira = await ApiCliente.JsonAsync(pagina1);
        var segunda = await ApiCliente.JsonAsync(pagina2);
        Assert.AreEqual(21, primeira.GetProperty("total").GetInt32());
        Assert.AreEqual(20, primeira.GetProperty("items").GetArrayLength());
        Assert.AreEqual(1, segunda.GetProperty("items").GetArrayLength());
        using var edicao = await cliente.GetAsync($"/api/v1/veiculos?somenteDisponiveis=true&aluguelEmEdicaoId={contrato.Id}&pageSize=100");
        var itens = (await ApiCliente.JsonAsync(edicao)).GetProperty("items").EnumerateArray().ToArray();
        Assert.AreEqual(22, itens.Length);
        Assert.IsTrue(itens.Any(v => v.GetProperty("id").GetGuid() == contrato.VeiculoId));
        var contratoB = Aluguel.Criar(b.Contexto.EmpresaId!.Value, b.Dados, CenarioLocacoesSql.Agora);
        b.Banco.Add(contratoB);
        await b.Banco.SaveChangesAsync();
        using var externo = await cliente.GetAsync($"/api/v1/veiculos?aluguelEmEdicaoId={contratoB.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, externo.StatusCode);
        using var condutores = await cliente.GetAsync($"/api/v1/condutores?clienteId={a.Dados.Cliente.Id}&pageSize=1&page=1");
        Assert.AreEqual(1, (await ApiCliente.JsonAsync(condutores)).GetProperty("total").GetInt32());
        using var filtroExterno = await cliente.GetAsync($"/api/v1/condutores?clienteId={b.Dados.Cliente.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, filtroExterno.StatusCode);
        using var semParametro = await cliente.GetAsync("/api/v1/condutores");
        Assert.AreEqual(1, (await ApiCliente.JsonAsync(semParametro)).GetProperty("total").GetInt32());
        for (var i = 1; i <= 21; i++)
            a.Banco.Add(Condutor.Novo(a.Contexto.EmpresaId.Value,
                new(a.Dados.Cliente.Id, $"Condutor sintético {i:D2}", "teste@example.invalid", CpfSintetico(i),
                    "CNH-SINTETICA", new(2028, 1, 1), "000"), CenarioLocacoesSql.Agora));
        await a.Banco.SaveChangesAsync();
        using var segundaCondutores = await cliente.GetAsync($"/api/v1/condutores?clienteId={a.Dados.Cliente.Id}&page=2");
        var segundaCondutoresJson = await ApiCliente.JsonAsync(segundaCondutores);
        Assert.AreEqual(22, segundaCondutoresJson.GetProperty("total").GetInt32());
        Assert.AreEqual(2, segundaCondutoresJson.GetProperty("items").GetArrayLength());
        using var semSessao = Clientes.Criar();
        using var negado = await semSessao.GetAsync("/api/v1/veiculos?somenteDisponiveis=true");
        Assert.AreEqual(HttpStatusCode.Unauthorized, negado.StatusCode);
    }

    internal static ComandoContratacao Comando(CenarioLocacoesSql cenario) => new(cenario.Dados.Cliente.Id,
        cenario.Dados.Condutor.Id, cenario.Dados.Veiculo.Id, cenario.Dados.Plano.Id, cenario.Dados.RetornoPrevisto,
        cenario.Dados.HodometroInicial, cenario.Dados.Taxas.Select(t => t.Id).ToArray(), cenario.Dados.Seguros.Select(s => s.Tipo).ToArray());

    internal static async Task<HttpClient> AutenticarAsync(CenarioLocacoesSql cenario, FabricaApi? fabrica = null)
    {
        var agora = DateTimeOffset.UtcNow;
        var sessao = SessaoAutenticacao.Abrir(cenario.Contexto.ContaId!.Value, cenario.Contexto.EmpresaId!.Value, agora);
        cenario.Banco.Add(sessao);
        await cenario.Banco.SaveChangesAsync();
        var cliente = Clientes.Criar(fabrica);
        ApiCliente.Bearer(cliente, TokenTeste.Emitir(cenario.Contexto.ContaId.Value, cenario.Contexto.EmpresaId.Value, sessao.Id, agora.AddMinutes(10)));
        return cliente;
    }

    private sealed class Relogio : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => CenarioLocacoesSql.Agora.AddMinutes(3);
    }

    private static string CpfSintetico(int numero)
    {
        var texto = "123" + numero.ToString("D6");
        for (var tamanho = 9; tamanho <= 10; tamanho++)
        {
            var soma = texto.Select((c, i) => (c - '0') * (tamanho + 1 - i)).Sum();
            var resto = soma % 11;
            texto += (resto < 2 ? 0 : 11 - resto).ToString();
        }
        return texto;
    }
}
