using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class EdicaoConcorrenciaTests
{
    [TestMethod]
    public async Task PlanoDeOutroGrupoRejeitadoSemNovaRevisao()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var outroGrupo = Grupo.Novo(cenario.Contexto.EmpresaId!.Value, "Outro grupo", CenarioLocacoesSql.Agora);
        var planoExterno = Plano.Novo(outroGrupo.EmpresaId, outroGrupo.Id, ModalidadePlano.Livre, 10, null, null, null, CenarioLocacoesSql.Agora);
        cenario.Banco.AddRange(outroGrupo, planoExterno);
        await cenario.Banco.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            EdicaoLocacoesTests.Casos(cenario.Banco, cenario.Contexto).AlterarAsync(criado.AluguelId,
                comando with { PlanoId = planoExterno.Id }, VersaoCadastro.Etag(criado.RowVersion), CancellationToken.None));
        Assert.AreEqual(1, await cenario.Banco.RevisoesContrato.CountAsync());
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(comando.PlanoId, (await cenario.Banco.Alugueis.SingleAsync()).PlanoId);
    }

    [TestMethod]
    public async Task DuasEdicoesConcorrentesMesmaVersaoProduzemUmaRevisaoEConflitoDeVersao()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var versao = VersaoCadastro.Etag(criado.RowVersion);
        cenario.Banco.ChangeTracker.Clear();
        await cenario.Banco.Database.CloseConnectionAsync();
        await using var banco1 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        await using var banco2 = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<object?> Editar(LocadoraDbContext banco, ComandoContratacao alteracao)
        {
            await inicio.Task;
            try
            {
                return await EdicaoLocacoesTests.Casos(banco, cenario.Contexto).AlterarAsync(criado.AluguelId, alteracao, versao, CancellationToken.None);
            }
            catch (PrecondicaoCadastroException erro)
            {
                Assert.IsFalse(erro.Ausente);
                return erro;
            }
        }
        var primeira = Editar(banco1, comando with { Seguros = [] });
        var segunda = Editar(banco2, comando with { RetornoPrevisto = comando.RetornoPrevisto.AddDays(1) });
        inicio.SetResult();
        var resultados = await Task.WhenAll(primeira, segunda);
        Assert.AreEqual(1, resultados.Count(r => r is ResultadoContratacao));
        Assert.AreEqual(1, resultados.Count(r => r is PrecondicaoCadastroException));
        Assert.AreEqual(2, await cenario.Banco.RevisoesContrato.CountAsync());
        Assert.AreEqual(2, await cenario.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task TrocaContraNovaContratacaoEmConexoesIndependentesSóUmaVence()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), CancellationToken.None);
        var reserva = Veiculo.Novo(cenario.Contexto.EmpresaId!.Value, new("FIC2B02", "Marca", "Preto", "Reserva",
            TipoCombustivel.Gasolina, 55, 2026, cenario.Dados.Grupo.Id), CenarioLocacoesSql.Agora);
        cenario.Banco.Add(reserva);
        await cenario.Banco.SaveChangesAsync();
        cenario.Banco.ChangeTracker.Clear();
        await cenario.Banco.Database.CloseConnectionAsync();
        await using var bancoTroca = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        await using var bancoCriacao = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        Assert.AreNotSame(bancoTroca.Database.GetDbConnection(), bancoCriacao.Database.GetDbConnection());
        var versao = VersaoCadastro.Etag(criado.RowVersion);
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Trocar()
        {
            await inicio.Task;
            try
            {
                await EdicaoLocacoesTests.Casos(bancoTroca, cenario.Contexto).AlterarAsync(criado.AluguelId,
                    comando with { VeiculoId = reserva.Id }, versao, CancellationToken.None);
                return true;
            }
            catch (ConflitoCadastroException erro)
            {
                Assert.AreEqual("veiculo_indisponivel", erro.Codigo);
                return false;
            }
        }
        async Task<bool> Contratar()
        {
            await inicio.Task;
            try
            {
                await ContratacaoSqlTests.Casos(bancoCriacao, cenario.Contexto).CriarAsync(
                    comando with { VeiculoId = reserva.Id }, Guid.NewGuid(), CancellationToken.None);
                return true;
            }
            catch (ConflitoCadastroException erro)
            {
                Assert.AreEqual("veiculo_indisponivel", erro.Codigo);
                return false;
            }
        }
        var trocar = Trocar();
        var contratar = Contratar();
        inicio.SetResult();
        var resultados = await Task.WhenAll(trocar, contratar);
        Assert.AreEqual(1, resultados.Count(r => r));
        var aluguel = await cenario.Banco.Alugueis.SingleAsync(a => a.Id == criado.AluguelId);
        var ocupacao = await cenario.Banco.Alugueis.CountAsync(a => a.Estado == EstadoAluguel.EmAberto && a.VeiculoId == reserva.Id);
        Assert.IsTrue(aluguel.VeiculoId == reserva.Id || ocupacao == 1);
        Assert.AreEqual(1, ocupacao);
    }

    [TestMethod]
    public async Task TrocaParaVeiculoOcupadoNaoLiberaVeiculoOriginal()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var baseComando = EstimativasSelecoesTests.Comando(cenario);
        var reserva = Veiculo.Novo(cenario.Contexto.EmpresaId!.Value, new("FIC3C03", "Marca", "Vermelho", "Bloqueado",
            TipoCombustivel.Diesel, 70, 2026, cenario.Dados.Grupo.Id), CenarioLocacoesSql.Agora);
        cenario.Banco.Add(reserva);
        await cenario.Banco.SaveChangesAsync();
        var principal = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto)
            .CriarAsync(baseComando, Guid.NewGuid(), CancellationToken.None);
        await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(
            baseComando with { VeiculoId = reserva.Id }, Guid.NewGuid(), CancellationToken.None);
        cenario.Banco.ChangeTracker.Clear();
        var excecao = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            EdicaoLocacoesTests.Casos(cenario.Banco, cenario.Contexto).AlterarAsync(principal.AluguelId,
                baseComando with { VeiculoId = reserva.Id }, VersaoCadastro.Etag(principal.RowVersion), CancellationToken.None));
        Assert.AreEqual("veiculo_indisponivel", excecao.Codigo);
        var aluguel = await cenario.Banco.Alugueis.SingleAsync(a => a.Id == principal.AluguelId);
        Assert.AreEqual(baseComando.VeiculoId, aluguel.VeiculoId);
        Assert.AreEqual(1, aluguel.NumeroRevisaoAtual);
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.EntidadeId == principal.AluguelId));
        Assert.IsTrue(await new ConsultasContratacao(cenario.Banco).VeiculoOcupadoAsync(baseComando.VeiculoId, null, CancellationToken.None));
    }
}