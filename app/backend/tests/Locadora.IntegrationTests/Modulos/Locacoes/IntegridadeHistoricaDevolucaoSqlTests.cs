using System.Text.Json;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Pessoas;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Infrastructure.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Pessoas;
using Locadora.Infrastructure.Modulos.Transversal;
using Microsoft.EntityFrameworkCore;
using static Locadora.IntegrationTests.Modulos.Locacoes.PersistenciaDevolucaoSqlTests;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class IntegridadeHistoricaDevolucaoSqlTests
{
    [TestMethod]
    public async Task ReajustesEMudancasCadastraisNaoAlteramRevisaoOuFinalRecomponivel()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        await FecharAsync(cenario, aluguel.Id, await PrepararDadosAsync(cenario));
        cenario.Banco.ChangeTracker.Clear();
        var consulta = new ConsultasAluguel(cenario.Banco, new RepositorioAlugueis(cenario.Banco));
        var antes = await consulta.ConsultarAsync(aluguel.Id, CancellationToken.None);
        var revisao = JsonSerializer.Serialize(antes.Revisao);
        var demonstrativo = JsonSerializer.Serialize(antes.Devolucao);
        await Unidade(cenario).ExecutarAsync(async token =>
        {
            var plano = await cenario.Banco.Planos.SingleAsync(token);
            plano.Alterar(999m, null, 999, 99m, Agora);
            foreach (var seguro in await cenario.Banco.PrecosSeguros.ToListAsync(token)) seguro.Alterar(99m, Agora);
            foreach (var taxa in await cenario.Banco.TaxasServicos.ToListAsync(token))
                taxa.Alterar("Reajustada", 999m, ModalidadeTaxa.Diaria, Agora);
            var veiculo = await cenario.Banco.Veiculos.SingleAsync(token);
            veiculo.Alterar(new(veiculo.Placa, "Marca nova", "Prata", "Modelo novo", TipoCombustivel.Diesel, 80m, 2026, veiculo.GrupoId), Agora);
            var cliente = await cenario.Banco.Clientes.SingleAsync(token);
            cliente.Alterar(new(TipoCliente.PF, "Outro nome", cliente.Cpf, null, cliente.Rg, cliente.Cnh,
                cliente.Telefone, cliente.Endereco, cliente.Email), Agora);
            cenario.Banco.Add(PrecoCombustivel.Novo(plano.EmpresaId, TipoCombustivel.Gasolina, 99m, Agora));
            return 0;
        }, CancellationToken.None);
        cenario.Banco.ChangeTracker.Clear();
        var depois = await consulta.ConsultarAsync(aluguel.Id, CancellationToken.None);
        Assert.AreEqual(revisao, JsonSerializer.Serialize(depois.Revisao));
        Assert.AreEqual(demonstrativo, JsonSerializer.Serialize(depois.Devolucao));
        Recompor(depois.Devolucao!, depois.Revisao);
        var planoAtual = await cenario.Banco.Planos.SingleAsync();
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            Planos(cenario).ExcluirPlanoAsync(planoAtual.Id, VersaoCadastro.Etag(planoAtual.RowVersion), CancellationToken.None));
        Assert.AreEqual("plano_ja_contratado", erro.Codigo);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReutilizacaoDeCpfCnpjEPlacaCriaOutraIdentidadeSemHerdarContrato(bool pessoaJuridica)
    {
        await using var baseCenario = await CenarioLocacoesSql.CriarAsync();
        var dados = baseCenario.Dados;
        if (pessoaJuridica)
        {
            var pj = await Unidade(baseCenario).ExecutarAsync(async token =>
            {
                var novo = Cliente.Novo(baseCenario.Contexto.EmpresaId!.Value, new(TipoCliente.PJ, "PJ sintética", null,
                    "98.700.000/0001-07", null, null, "000", dados.Cliente.Endereco, null), CenarioLocacoesSql.Agora);
                baseCenario.Banco.Add(novo);
                var condutor = await baseCenario.Banco.Condutores.SingleAsync(token);
                condutor.Alterar(new(novo.Id, condutor.Nome, condutor.Email, condutor.Cpf, condutor.Cnh,
                    condutor.ValidadeCnh, condutor.Telefone), CenarioLocacoesSql.Agora);
                return (Cliente: novo, Condutor: condutor);
            }, CancellationToken.None);
            dados = dados with { Cliente = pj.Cliente, Condutor = pj.Condutor };
        }
        var cenario = new CenarioLocacoesSql(baseCenario.Contexto, baseCenario.Banco, dados);
        var aluguel = await CriarAbertoAsync(cenario);
        await FecharAsync(cenario, aluguel.Id, await PrepararDadosAsync(cenario));
        var pessoas = Pessoas(cenario);
        var veiculos = Veiculos(cenario);
        var condutorOriginal = await pessoas.CondutorAsync(dados.Condutor.Id, CancellationToken.None);
        await pessoas.ExcluirCondutorAsync(condutorOriginal.Id, VersaoCadastro.Etag(condutorOriginal.RowVersion), CancellationToken.None);
        var clienteOriginal = await pessoas.ClienteAsync(dados.Cliente.Id, CancellationToken.None);
        await pessoas.ExcluirClienteAsync(clienteOriginal.Id, VersaoCadastro.Etag(clienteOriginal.RowVersion), CancellationToken.None);
        var veiculoOriginal = await veiculos.ConsultarAsync(dados.Veiculo.Id, CancellationToken.None);
        await veiculos.ExcluirAsync(veiculoOriginal.Id, VersaoCadastro.Etag(veiculoOriginal.RowVersion), CancellationToken.None);
        var novoCliente = await pessoas.CriarClienteAsync(new(dados.Cliente.Tipo, "Nova identidade", dados.Cliente.Cpf,
            dados.Cliente.Cnpj, dados.Cliente.Rg, dados.Cliente.Cnh, dados.Cliente.Telefone,
            dados.Cliente.Endereco, dados.Cliente.Email), CancellationToken.None);
        var novoCondutor = await pessoas.CriarCondutorAsync(new(novoCliente.Id, "Nova identidade condutora",
            dados.Condutor.Email, dados.Condutor.Cpf, dados.Condutor.Cnh, dados.Condutor.ValidadeCnh, dados.Condutor.Telefone),
            !pessoaJuridica, CancellationToken.None);
        var novoVeiculo = await veiculos.CriarAsync(new(dados.Veiculo.Placa, "Nova marca", "Azul", "Nova identidade",
            dados.Veiculo.Combustivel, dados.Veiculo.CapacidadeLitros, dados.Veiculo.Ano, dados.Veiculo.GrupoId), CancellationToken.None);
        Assert.AreNotEqual(dados.Cliente.Id, novoCliente.Id);
        Assert.AreNotEqual(dados.Condutor.Id, novoCondutor.Id);
        Assert.AreNotEqual(dados.Veiculo.Id, novoVeiculo.Id);
        Assert.IsFalse(await cenario.Banco.Alugueis.AnyAsync(a => a.ClienteId == novoCliente.Id || a.CondutorId == novoCondutor.Id || a.VeiculoId == novoVeiculo.Id));
        var consulta = new ConsultasAluguel(cenario.Banco, new RepositorioAlugueis(cenario.Banco));
        Assert.AreEqual(0, (await consulta.ListarAsync(new(1, 20), new(VeiculoId: novoVeiculo.Id), CancellationToken.None)).Total);
        Assert.AreEqual(1, (await consulta.ListarAsync(new(1, 20), new(VeiculoId: dados.Veiculo.Id), CancellationToken.None)).Total);
        var antigo = await consulta.ConsultarAsync(aluguel.Id, CancellationToken.None);
        Assert.AreEqual(dados.Cliente.Id, antigo.Revisao.ClienteId);
        Assert.AreEqual(dados.Condutor.Id, antigo.Revisao.CondutorId);
        Assert.AreEqual(dados.Veiculo.Id, antigo.Revisao.VeiculoId);
        Recompor(antigo.Devolucao!, antigo.Revisao);
        await OcultacaoDevolucaoSqlTests.Casos(cenario.Banco, cenario.Contexto)
            .OcultarAsync(aluguel.Id, VersaoCadastro.Etag(antigo.RowVersion), CancellationToken.None);
        var oculto = await consulta.ConsultarAsync(aluguel.Id, true, CancellationToken.None);
        Assert.IsTrue(oculto.Oculto);
        Assert.AreEqual(dados.Cliente.Id, oculto.Revisao.ClienteId);
        Assert.AreEqual(dados.Cliente.Cnpj, oculto.Revisao.Cliente.Cnpj);
        Assert.AreEqual(dados.Cliente.Cpf, oculto.Revisao.Cliente.Cpf);
        Recompor(oculto.Devolucao!, oculto.Revisao);
        var plano = await cenario.Banco.Planos.SingleAsync();
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            Planos(cenario).ExcluirPlanoAsync(plano.Id, VersaoCadastro.Etag(plano.RowVersion), CancellationToken.None));
    }

    private static DateTimeOffset Agora => CenarioLocacoesSql.Agora.AddDays(3);
    private sealed class Relogio : TimeProvider { public override DateTimeOffset GetUtcNow() => Agora; }

    private static CasosPessoas Pessoas(CenarioLocacoesSql cenario) => new(new RepositorioPessoas(cenario.Banco),
        Unidade(cenario), new RegistroAuditoriaCadastro(cenario.Banco, cenario.Contexto, new Relogio()),
        cenario.Contexto, new VinculosAluguelPessoas(cenario.Banco), new Relogio());

    private static CasosVeiculos Veiculos(CenarioLocacoesSql cenario) => new(new RepositorioVeiculos(cenario.Banco),
        new RepositorioGruposPlanos(cenario.Banco), Unidade(cenario),
        new RegistroAuditoriaCadastro(cenario.Banco, cenario.Contexto, new Relogio()),
        cenario.Contexto, new VinculosAluguelVeiculo(cenario.Banco), new Relogio());

    private static CasosGruposPlanos Planos(CenarioLocacoesSql cenario) => new(new RepositorioGruposPlanos(cenario.Banco),
        Unidade(cenario), new RegistroAuditoriaCadastro(cenario.Banco, cenario.Contexto, new Relogio()),
        cenario.Contexto, new VinculosAluguelPlano(cenario.Banco), new Relogio());

    private static void Recompor(DemonstrativoFinal final, RevisaoContrato revisao)
    {
        var adicionais = revisao.Itens.Select(i => new AdicionalCalculoFinal(i.ParaCalculo(), i.OrigemInclusao))
            .Concat(final.Itens.Select(i => new AdicionalCalculoFinal(new(CategoriaItem.TaxaServico, i.TaxaServicoId,
                null, i.Nome, i.Modalidade, i.PrecoUnitario), i.OrigemInclusao))).ToArray();
        var recalculo = CalculadoraDevolucao.Calcular(new(final.SaidaUtc, final.RetornoPrevistoUtc, final.RetornoRealUtc,
            final.HodometroInicial, final.HodometroFinal, final.PlanoId, final.Plano.Parametros(),
            final.TipoCombustivel, final.CapacidadeLitros, final.LitrosRestantes,
            new(final.PrecoCombustivelId, final.TipoCombustivel, final.InicioVigenciaCombustivelUtc, final.PrecoCombustivel), adicionais));
        Assert.AreEqual(final.TotalFinal, recalculo.TotalFinal);
        Assert.AreEqual(334m, final.TotalFinal);
        Assert.AreEqual(final.TotalFinal, final.Componentes.Sum(c => c.ValorArredondado));
        Assert.AreEqual(final.Diarias, recalculo.Diarias);
        Assert.AreEqual(final.Quilometros, recalculo.Quilometros);
        Assert.AreEqual(final.TermoDiariasBruto, recalculo.TermoDiariasBruto);
        Assert.AreEqual(final.TermoKmBruto, recalculo.TermoKmBruto);
    }
}
