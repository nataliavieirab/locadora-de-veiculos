using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class ContratacaoSqlTests
{
    [TestMethod]
    public async Task ConfirmaComRelogioPrecosAtuaisArvoreEAuditoriaNoMesmoCommit()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var tempo = new Relogio();
        var previa = await new CasosEstimativas(new ConsultasContratacao(cenario.Banco), cenario.Contexto, tempo)
            .EstimarAsync(comando, null, null, CancellationToken.None);
        cenario.Dados.Plano.Alterar(120, null, 100, 2, tempo.Agora.AddMinutes(1));
        await cenario.Banco.SaveChangesAsync();
        tempo.Agora = tempo.Agora.AddMinutes(1);
        var resultado = await Casos(cenario.Banco, cenario.Contexto, tempo).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        Assert.AreNotEqual(previa.SaidaUtc, resultado.Revisao.SaidaUtc);
        Assert.AreEqual(254m, previa.Revisao.Estimativa.TotalConhecido);
        Assert.AreEqual(294m, resultado.Revisao.Estimativa.TotalConhecido);
        Assert.AreEqual(tempo.Agora, resultado.Revisao.SaidaUtc);
        Assert.AreEqual(8, resultado.RowVersion.Length);
        Assert.AreEqual(1, await cenario.Banco.Alugueis.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.RevisoesContrato.CountAsync());
        Assert.AreEqual(3, await cenario.Banco.ItensContratados.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.DemonstrativosEstimados.CountAsync());
        Assert.AreEqual(4, await cenario.Banco.ComponentesEstimativa.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.ComandosIdempotentes.CountAsync());
        var auditoria = await cenario.Banco.AuditoriasCadastro.SingleAsync();
        Assert.AreEqual(resultado.AluguelId, auditoria.EntidadeId);
        Assert.AreEqual(cenario.Contexto.ContaId, auditoria.ContaId);
        Assert.AreEqual(AcaoCadastro.Criacao, auditoria.Acao);
    }

    [TestMethod]
    public async Task ClientePjExigeCondutorPfDiretamenteVinculado()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var pj = Cliente.Novo(cenario.Contexto.EmpresaId!.Value, new(TipoCliente.PJ, "PJ sintética", null,
            "98.700.000/0001-07", null, null, "000", cenario.Dados.Cliente.Endereco, null), CenarioLocacoesSql.Agora);
        cenario.Banco.Add(pj);
        await cenario.Banco.SaveChangesAsync();
        var casos = Casos(cenario.Banco, cenario.Contexto);
        var comando = EstimativasSelecoesTests.Comando(cenario) with { ClienteId = pj.Id };
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() => casos.CriarAsync(comando, Guid.NewGuid(), CancellationToken.None));
        cenario.Banco.ChangeTracker.Clear();
        var condutor = await cenario.Banco.Condutores.SingleAsync();
        condutor.Alterar(new(pj.Id, condutor.Nome, condutor.Email, condutor.Cpf, condutor.Cnh,
            condutor.ValidadeCnh, condutor.Telefone), CenarioLocacoesSql.Agora);
        await cenario.Banco.SaveChangesAsync();
        var resultado = await casos.CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        Assert.AreEqual(TipoCliente.PJ, resultado.Revisao.Cliente.Tipo);
        Assert.AreEqual(pj.Id, resultado.Revisao.ClienteId);
        Assert.AreEqual(condutor.Id, resultado.Revisao.CondutorId);
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task ReferenciasExternasPrazoEHodometroInvalidosNaoCriamEfeitos()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var casos = Casos(a.Banco, a.Contexto);
        var comando = EstimativasSelecoesTests.Comando(a);
        foreach (var externo in new[]
        {
            comando with { ClienteId = b.Dados.Cliente.Id }, comando with { CondutorId = b.Dados.Condutor.Id },
            comando with { VeiculoId = b.Dados.Veiculo.Id }, comando with { PlanoId = b.Dados.Plano.Id },
            comando with { TaxasServicoIds = [b.Dados.Taxas.Single().Id] },
        })
            await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => casos.CriarAsync(externo, Guid.NewGuid(), CancellationToken.None));
        foreach (var invalido in new[] { comando with { RetornoPrevisto = CenarioLocacoesSql.Agora }, comando with { HodometroInicial = -1 } })
            await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() => casos.CriarAsync(invalido, Guid.NewGuid(), CancellationToken.None));
        Assert.AreEqual(0, await a.Banco.Alugueis.CountAsync());
        Assert.AreEqual(0, await a.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(0, await a.Banco.ComandosIdempotentes.CountAsync());
    }

    [TestMethod]
    public async Task DuasCriacoesComChavesDistintasEmConexoesIndependentesProduzemUmAberto()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        await using var banco1 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        await using var banco2 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        Assert.AreNotSame(banco1.Database.GetDbConnection(), banco2.Database.GetDbConnection());
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Tentar(LocadoraDbContext banco)
        {
            await inicio.Task;
            try
            {
                await Casos(banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
                return true;
            }
            catch (ConflitoCadastroException erro)
            {
                Assert.AreEqual("veiculo_indisponivel", erro.Codigo);
                return false;
            }
        }
        var primeira = Tentar(banco1);
        var segunda = Tentar(banco2);
        inicio.SetResult();
        var resultados = await Task.WhenAll(primeira, segunda);
        Assert.AreEqual(1, resultados.Count(r => r));
        Assert.AreEqual(1, await cenario.Banco.Alugueis.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.RevisoesContrato.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.ComandosIdempotentes.CountAsync());
    }

    [TestMethod]
    public async Task FalhaAposCadaSaveAntesDoCommitReverteTudo()
    {
        foreach (var falharNoSave in new[] { 1, 2 })
        {
            await using var cenario = await CenarioLocacoesSql.CriarAsync();
            var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>().UseSqlServer(AmbienteIntegracao.Conexao)
                .AddInterceptors(new GuardaEscritaEmpresa(cenario.Contexto), new FalhaAposSave(falharNoSave)).Options;
            await using var banco = new LocadoraDbContext(opcoes, cenario.Contexto);
            var chave = Guid.NewGuid();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                Casos(banco, cenario.Contexto).CriarAsync(EstimativasSelecoesTests.Comando(cenario), chave, CancellationToken.None));
            Assert.AreEqual(0, await cenario.Banco.Alugueis.CountAsync());
            Assert.AreEqual(0, await cenario.Banco.RevisoesContrato.CountAsync());
            Assert.AreEqual(0, await cenario.Banco.ItensContratados.CountAsync());
            Assert.AreEqual(0, await cenario.Banco.DemonstrativosEstimados.CountAsync());
            Assert.AreEqual(0, await cenario.Banco.ComponentesEstimativa.CountAsync());
            Assert.AreEqual(0, await cenario.Banco.AuditoriasCadastro.CountAsync());
            Assert.AreEqual(0, await cenario.Banco.ComandosIdempotentes.CountAsync());
            await Casos(cenario.Banco, cenario.Contexto).CriarAsync(EstimativasSelecoesTests.Comando(cenario), chave, CancellationToken.None);
            Assert.AreEqual(1, await cenario.Banco.Alugueis.CountAsync());
            Assert.AreEqual(1, await cenario.Banco.ComandosIdempotentes.CountAsync());
        }
    }

    internal static CasosContratacao Casos(LocadoraDbContext banco, ContextoEmpresa contexto, TimeProvider? tempo = null)
    {
        tempo ??= new Relogio();
        return new(new ConsultasContratacao(banco), new RepositorioAlugueis(banco),
            new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
            new RegistroAuditoriaCadastro(banco, contexto, tempo), contexto, tempo);
    }

    internal sealed class Relogio : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = CenarioLocacoesSql.Agora;
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed class FalhaAposSave(int falharNoSave) : SaveChangesInterceptor
    {
        private int _chamadas;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (++_chamadas == falharNoSave) throw new InvalidOperationException("Falha sintética antes do commit.");
            return ValueTask.FromResult(result);
        }
    }
}
