using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class EdicaoLocacoesTests
{
    [TestMethod]
    public async Task PrazoAposReajustePreservaPrecoSaidaERevisaoAnterior()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var plano = await cenario.Banco.Planos.SingleAsync();
        var seguro = await cenario.Banco.PrecosSeguros.SingleAsync(s => s.Tipo == TipoSeguro.Cliente);
        plano.Alterar(900, null, 200, 9, CenarioLocacoesSql.Agora.AddMinutes(1));
        seguro.Alterar(99, CenarioLocacoesSql.Agora.AddMinutes(1));
        await cenario.Banco.SaveChangesAsync();
        var resultado = await Casos(cenario.Banco, cenario.Contexto).AlterarAsync(criado.AluguelId,
            comando with { RetornoPrevisto = comando.RetornoPrevisto.AddDays(1) }, VersaoCadastro.Etag(criado.RowVersion), CancellationToken.None);
        Assert.AreEqual(2, resultado.Revisao.Numero);
        Assert.AreEqual(CenarioLocacoesSql.Agora, resultado.Revisao.SaidaUtc);
        Assert.AreEqual(366m, resultado.Revisao.Estimativa.TotalConhecido);
        Assert.AreEqual(100m, resultado.Revisao.Plano.PrecoDiario);
        Assert.AreEqual(5m, resultado.Revisao.Itens.Single(i => i.TipoSeguro == TipoSeguro.Cliente).PrecoUnitario);
        Assert.IsFalse(criado.RowVersion.SequenceEqual(resultado.RowVersion));
        var primeira = await cenario.Banco.RevisoesContrato.Include(r => r.Itens).Include(r => r.Estimativa).SingleAsync(r => r.Numero == 1);
        Assert.AreEqual(254m, primeira.Estimativa.TotalConhecido);
        Assert.AreEqual(comando.RetornoPrevisto, primeira.RetornoPrevistoUtc);
        Assert.AreEqual(2, await cenario.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task PlanoSubstituidoEAdicionalReincluidoUsamPrecoAtual()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var plano = Plano.Novo(cenario.Contexto.EmpresaId!.Value, cenario.Dados.Grupo.Id,
            ModalidadePlano.Diario, 150, 3, null, null, CenarioLocacoesSql.Agora);
        cenario.Banco.Add(plano);
        await cenario.Banco.SaveChangesAsync();
        var casos = Casos(cenario.Banco, cenario.Contexto);
        var segunda = await casos.AlterarAsync(criado.AluguelId, comando with { PlanoId = plano.Id, TaxasServicoIds = [] },
            VersaoCadastro.Etag(criado.RowVersion), CancellationToken.None);
        Assert.AreEqual(150m, segunda.Revisao.Plano.PrecoDiario);
        Assert.AreEqual(324m, segunda.Revisao.Estimativa.TotalConhecido);
        var taxa = await cenario.Banco.TaxasServicos.SingleAsync();
        taxa.Alterar("Taxa reajustada", 60, ModalidadeTaxa.Fixa, CenarioLocacoesSql.Agora.AddMinutes(1));
        await cenario.Banco.SaveChangesAsync();
        var terceira = await casos.AlterarAsync(criado.AluguelId, comando with { PlanoId = plano.Id },
            VersaoCadastro.Etag(segunda.RowVersion), CancellationToken.None);
        var reincluida = terceira.Revisao.Itens.Single(i => i.Categoria == CategoriaItem.TaxaServico);
        Assert.AreEqual(60m, reincluida.PrecoUnitario);
        Assert.AreEqual(3, reincluida.RevisaoPrimeiraInclusao);
        Assert.AreEqual(OrigemInclusao.Edicao, reincluida.OrigemInclusao);
        Assert.AreEqual(384m, terceira.Revisao.Estimativa.TotalConhecido);
        Assert.AreEqual(30m, (await cenario.Banco.ItensContratados.SingleAsync(i => i.RevisaoId == criado.Revisao.Id && i.Categoria == CategoriaItem.TaxaServico)).PrecoUnitario);
    }

    [TestMethod]
    public async Task EtagAusenteObsoletoOuEdicaoInvalidaNaoAlteraRaizRevisoesOuAuditoria()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var casos = Casos(cenario.Banco, cenario.Contexto);
        var ausente = await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() => casos.AlterarAsync(criado.AluguelId, comando, null, CancellationToken.None));
        Assert.IsTrue(ausente.Ausente);
        var atualizado = await casos.AlterarAsync(criado.AluguelId, comando with { Seguros = [] }, VersaoCadastro.Etag(criado.RowVersion), CancellationToken.None);
        var obsoleto = await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() => casos.AlterarAsync(criado.AluguelId, comando, VersaoCadastro.Etag(criado.RowVersion), CancellationToken.None));
        Assert.IsFalse(obsoleto.Ausente);
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() => casos.AlterarAsync(criado.AluguelId,
            comando with { RetornoPrevisto = CenarioLocacoesSql.Agora }, VersaoCadastro.Etag(atualizado.RowVersion), CancellationToken.None));
        Assert.AreEqual(2, await cenario.Banco.RevisoesContrato.CountAsync());
        Assert.AreEqual(2, await cenario.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(2, (await cenario.Banco.Alugueis.SingleAsync()).NumeroRevisaoAtual);
        Assert.AreEqual(0, atualizado.Revisao.Itens.Count(i => i.Categoria == CategoriaItem.Seguro));
    }

    [TestMethod]
    public async Task TrocaDeVeiculoPreservaIdentidadeAnteriorSemOcupacaoCorrente()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var novo = Veiculo.Novo(cenario.Contexto.EmpresaId!.Value, new("FIC2A02", "Outra marca", "Prata", "Outro modelo",
            TipoCombustivel.Alcool, 60, 2026, cenario.Dados.Grupo.Id), CenarioLocacoesSql.Agora);
        cenario.Banco.Add(novo);
        await cenario.Banco.SaveChangesAsync();
        await Casos(cenario.Banco, cenario.Contexto).AlterarAsync(criado.AluguelId, comando with { VeiculoId = novo.Id },
            VersaoCadastro.Etag(criado.RowVersion), CancellationToken.None);
        var consultas = new ConsultasContratacao(cenario.Banco);
        Assert.IsFalse(await consultas.VeiculoOcupadoAsync(comando.VeiculoId, null, CancellationToken.None));
        Assert.IsTrue(await consultas.VeiculoOcupadoAsync(novo.Id, null, CancellationToken.None));
        Assert.AreEqual(comando.VeiculoId, (await cenario.Banco.RevisoesContrato.SingleAsync(r => r.Numero == 1)).VeiculoId);
        Assert.AreEqual(novo.Id, (await cenario.Banco.Alugueis.SingleAsync()).VeiculoId);
    }

    internal static CasosEdicaoAluguel Casos(LocadoraDbContext banco, ContextoEmpresa contexto)
        => new(new ConsultasContratacao(banco), new RepositorioAlugueis(banco),
            new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
            new RegistroAuditoriaCadastro(banco, contexto, new ContratacaoSqlTests.Relogio { Agora = CenarioLocacoesSql.Agora.AddMinutes(3) }),
            new ContratacaoSqlTests.Relogio { Agora = CenarioLocacoesSql.Agora.AddMinutes(3) });
}
