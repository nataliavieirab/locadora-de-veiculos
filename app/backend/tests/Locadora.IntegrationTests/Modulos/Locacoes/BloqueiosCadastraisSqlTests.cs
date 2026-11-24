using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Pessoas;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Pessoas;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Locadora.IntegrationTests.Suporte;
using Locadora.Application.Modulos.Locacoes;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class BloqueiosCadastraisSqlTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [TestMethod]
    public async Task F3_CT_10_BloqueiaCadastrosComprometidosSemEfeitoParcial()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), Token);
        var casos = Fabrica(cenario);
        var antesAuditoria = await cenario.Banco.AuditoriasCadastro.CountAsync();

        var veiculo = cenario.Dados.Veiculo;
        var erroVeiculo = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Veiculos.AlterarAsync(veiculo.Id,
                new(veiculo.Placa, veiculo.Marca, "Prata", veiculo.Modelo, veiculo.Combustivel,
                    veiculo.CapacidadeLitros, veiculo.Ano, veiculo.GrupoId), Versao(veiculo), Token));
        Assert.AreEqual("veiculo_com_aluguel_aberto", erroVeiculo.Codigo);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Veiculos.ExcluirAsync(veiculo.Id, Versao(veiculo), Token));

        var cliente = cenario.Dados.Cliente;
        var erroCliente = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Pessoas.ExcluirClienteAsync(cliente.Id, Versao(cliente), Token));
        Assert.AreEqual("cliente_com_aluguel_aberto", erroCliente.Codigo);

        var condutor = cenario.Dados.Condutor;
        var erroCondutor = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Pessoas.AlterarCondutorAsync(condutor.Id,
                new(condutor.ClienteId, condutor.Nome, condutor.Email, condutor.Cpf, "OUTRA-CNH",
                    condutor.ValidadeCnh.AddDays(1), condutor.Telefone), true, Versao(condutor), Token));
        Assert.AreEqual("condutor_com_aluguel_aberto", erroCondutor.Codigo);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Pessoas.ExcluirCondutorAsync(condutor.Id, Versao(condutor), Token));

        var taxa = cenario.Dados.Taxas.Single();
        var erroTaxa = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Taxas.AlterarAsync(taxa.Id, new("Outro nome", 9m, ModalidadeTaxa.Diaria), Versao(taxa), Token));
        Assert.AreEqual("taxa_com_aluguel_aberto", erroTaxa.Codigo);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => casos.Taxas.ExcluirAsync(taxa.Id, Versao(taxa), Token));

        var plano = cenario.Dados.Plano;
        var erroPlano = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Planos.ExcluirPlanoAsync(plano.Id, Versao(plano), Token));
        Assert.AreEqual("plano_ja_contratado", erroPlano.Codigo);

        Assert.AreEqual("Azul", (await casos.Veiculos.ConsultarAsync(veiculo.Id, Token)).Cor);
        CollectionAssert.AreEqual(veiculo.RowVersion, (await casos.Veiculos.ConsultarAsync(veiculo.Id, Token)).RowVersion);
        Assert.AreEqual(30m, (await casos.Taxas.ConsultarAsync(taxa.Id, Token)).Preco);
        Assert.AreEqual(antesAuditoria, await cenario.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task F3_CT_10_ClientePfNaoContornaBloqueioDoCondutorNaCnh()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(
            EstimativasSelecoesTests.Comando(cenario), Guid.NewGuid(), Token);
        var casos = Fabrica(cenario);
        var cliente = cenario.Dados.Cliente;
        var antes = await cenario.Banco.AuditoriasCadastro.CountAsync();
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Pessoas.AlterarClienteAsync(cliente.Id, new(TipoCliente.PF, cliente.Nome, cliente.Cpf, null,
                cliente.Rg, "CNH-ALTERADA", cliente.Telefone, cliente.Endereco, cliente.Email), Versao(cliente), Token));
        Assert.AreEqual("condutor_com_aluguel_aberto", erro.Codigo);
        Assert.AreEqual(antes, await cenario.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(cliente.CnhNormalizada, (await casos.Pessoas.ClienteAsync(cliente.Id, Token)).CnhNormalizada);
    }

    [TestMethod]
    public async Task F3_CT_10_TrocaDeVeiculoLiberaIdentidadeAnterior()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), Token);
        var novo = Veiculo.Novo(cenario.Contexto.EmpresaId!.Value, new("FIC2A02", "Marca", "Prata", "Modelo",
            TipoCombustivel.Gasolina, 50, 2026, cenario.Dados.Grupo.Id), CenarioLocacoesSql.Agora);
        cenario.Banco.Add(novo);
        await cenario.Banco.SaveChangesAsync();
        await EdicaoLocacoesTests.Casos(cenario.Banco, cenario.Contexto).AlterarAsync(criado.AluguelId,
            comando with { VeiculoId = novo.Id }, VersaoCadastro.Etag(criado.RowVersion), Token);

        var casos = Fabrica(cenario);
        var original = cenario.Dados.Veiculo;
        var alterado = await casos.Veiculos.AlterarAsync(original.Id,
            new(original.Placa, original.Marca, "Verde", original.Modelo, original.Combustivel,
                original.CapacidadeLitros, original.Ano, original.GrupoId), Versao(original), Token);
        Assert.AreEqual("Verde", alterado.Cor);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Veiculos.AlterarAsync(novo.Id,
                new(novo.Placa, novo.Marca, "Vermelho", novo.Modelo, novo.Combustivel,
                    novo.CapacidadeLitros, novo.Ano, novo.GrupoId), VersaoCadastro.Etag(novo.RowVersion), Token));
    }

    [TestMethod]
    public async Task F3_CT_10_TaxaRemovidaEPlanoHistoricoMantemRegrasCorretas()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var criado = await ContratacaoSqlTests.Casos(cenario.Banco, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), Token);
        var planoNovo = Plano.Novo(cenario.Contexto.EmpresaId!.Value, cenario.Dados.Grupo.Id,
            ModalidadePlano.Diario, 150, 3, null, null, CenarioLocacoesSql.Agora);
        cenario.Banco.Add(planoNovo);
        await cenario.Banco.SaveChangesAsync();
        var segunda = await EdicaoLocacoesTests.Casos(cenario.Banco, cenario.Contexto).AlterarAsync(criado.AluguelId,
            comando with { PlanoId = planoNovo.Id, TaxasServicoIds = [] }, VersaoCadastro.Etag(criado.RowVersion), Token);

        var casos = Fabrica(cenario);
        var taxa = cenario.Dados.Taxas.Single();
        await casos.Taxas.AlterarAsync(taxa.Id, new("Taxa liberada", 40m, ModalidadeTaxa.Fixa), Versao(taxa), Token);

        var planoOriginal = cenario.Dados.Plano;
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Planos.ExcluirPlanoAsync(planoOriginal.Id, Versao(planoOriginal), Token));
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.Planos.ExcluirPlanoAsync(planoNovo.Id, Versao(planoNovo), Token));
        Assert.AreEqual(2, segunda.Revisao.Numero);
        Assert.AreEqual(2, await cenario.Banco.RevisoesContrato.CountAsync());
        Assert.AreEqual("Taxa liberada", (await casos.Taxas.ConsultarAsync(taxa.Id, Token)).Nome);
    }

    [TestMethod]
    [DataRow("veiculo")]
    [DataRow("condutor")]
    [DataRow("taxa")]
    [DataRow("cliente")]
    public async Task ContratacaoContraMutacaoCadastralEmConexoesIndependentesPreservaInvariantes(string alvo)
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var comando = EstimativasSelecoesTests.Comando(cenario);
        var veiculo = cenario.Dados.Veiculo;
        var condutor = cenario.Dados.Condutor;
        var cliente = cenario.Dados.Cliente;
        var taxa = cenario.Dados.Taxas.Single();
        await cenario.Banco.Database.CloseConnectionAsync();
        cenario.Banco.ChangeTracker.Clear();
        await using var bancoContrato = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        await using var bancoCadastro = AmbienteIntegracao.CriarContextoComGuarda(cenario.Contexto);
        Assert.AreNotSame(bancoContrato.Database.GetDbConnection(), bancoCadastro.Database.GetDbConnection());
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Contratar()
        {
            await inicio.Task;
            try
            {
                await ContratacaoSqlTests.Casos(bancoContrato, cenario.Contexto).CriarAsync(comando, Guid.NewGuid(), Token);
                return true;
            }
            catch (Exception erro) when (erro is CadastroNaoEncontradoException or ValidacaoCadastroException)
            {
                return false;
            }
        }
        async Task<bool> AlterarCadastro()
        {
            await inicio.Task;
            var casos = Fabrica(bancoCadastro, cenario.Contexto);
            try
            {
                switch (alvo)
                {
                    case "veiculo":
                        await casos.Veiculos.ExcluirAsync(veiculo.Id, Versao(veiculo), Token);
                        break;
                    case "condutor":
                        await casos.Pessoas.AlterarCondutorAsync(condutor.Id,
                            new(condutor.ClienteId, condutor.Nome, condutor.Email, condutor.Cpf, condutor.Cnh,
                                DateOnly.FromDateTime(CenarioLocacoesSql.Agora.Date).AddDays(-1), condutor.Telefone),
                            true, Versao(condutor), Token);
                        break;
                    case "taxa":
                        await casos.Taxas.ExcluirAsync(taxa.Id, Versao(taxa), Token);
                        break;
                    case "cliente":
                        await casos.Pessoas.AlterarClienteAsync(cliente.Id,
                            new(TipoCliente.PF, cliente.Nome, cliente.Cpf, null, cliente.Rg,
                                "CNH-NOVA-SINTETICA", cliente.Telefone, cliente.Endereco, cliente.Email),
                            Versao(cliente), Token);
                        break;
                }
                return true;
            }
            catch (ConflitoCadastroException)
            {
                return false;
            }
        }
        var contratacao = Contratar();
        var cadastro = AlterarCadastro();
        inicio.SetResult();
        var resultados = await Task.WhenAll(contratacao, cadastro);
        if (alvo == "cliente") Assert.IsTrue(resultados[0]);
        else Assert.AreEqual(1, resultados.Count(sucesso => sucesso));
        Assert.AreEqual(resultados[0] ? 1 : 0, await cenario.Banco.Alugueis.CountAsync());
        Assert.AreEqual(resultados[0] ? 1 : 0, await cenario.Banco.ComandosIdempotentes.CountAsync());
        Assert.AreEqual(resultados[0] ? 1 : 0,
            await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Entidade == TipoCadastro.Aluguel));
        if (resultados[0])
        {
            var aluguel = await CenarioLocacoesSql.Contratos(cenario.Banco).SingleAsync();
            Assert.AreEqual(aluguel.RevisaoAtual.Cliente.Cnh, aluguel.RevisaoAtual.Condutor.Cnh);
            Assert.IsTrue(await cenario.Banco.Veiculos.AnyAsync(v => v.Id == aluguel.VeiculoId));
            Assert.IsTrue(await cenario.Banco.TaxasServicos.AnyAsync(t => t.Id == taxa.Id));
            Assert.IsTrue((await cenario.Banco.Condutores.SingleAsync(c => c.Id == condutor.Id)).ValidadeCnh
                >= DateOnly.FromDateTime(comando.RetornoPrevisto.Date));
        }
    }

    private static CasosCadastro Fabrica(CenarioLocacoesSql cenario)
        => Fabrica(cenario.Banco, cenario.Contexto);

    private static CasosCadastro Fabrica(LocadoraDbContext banco, ContextoEmpresa contexto)
    {
        var tempo = TimeProvider.System;
        var unidade = new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
        var auditoria = new RegistroAuditoriaCadastro(banco, contexto, tempo);
        var vinculosPessoas = new VinculosAluguelPessoas(banco);
        var vinculosPlano = new VinculosAluguelPlano(banco);
        var vinculosVeiculo = new VinculosAluguelVeiculo(banco);
        var vinculosTaxa = new VinculosAluguelTaxa(banco);
        return new(
            new CasosPessoas(new RepositorioPessoas(banco), unidade, auditoria, contexto, vinculosPessoas, tempo),
            new CasosVeiculos(new RepositorioVeiculos(banco), new RepositorioGruposPlanos(banco),
                unidade, auditoria, contexto, vinculosVeiculo, tempo),
            new CasosTaxasServicos(new RepositorioTaxasServicos(banco), unidade, auditoria, contexto, vinculosTaxa, tempo),
            new CasosGruposPlanos(new RepositorioGruposPlanos(banco), unidade, auditoria, contexto, vinculosPlano, tempo));
    }

    private static string Versao(EntidadeCadastro entidade) => VersaoCadastro.Etag(entidade.RowVersion);

    private sealed record CasosCadastro(CasosPessoas Pessoas, CasosVeiculos Veiculos, CasosTaxasServicos Taxas, CasosGruposPlanos Planos);
}
