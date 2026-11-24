using System.Text.Json;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.Locacoes;

[TestClass]
public sealed class FechamentoAluguelTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 24, 13, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void PreviaNaoAlteraRaizOuRevisoesEConclusaoConservaSnapshotLiteral()
    {
        var (aluguel, dados, devolucao) = Cenario();
        aluguel.Editar(dados with { HodometroInicial = 1001 }, Agora.AddMinutes(1));
        var snapshot = JsonSerializer.Serialize(aluguel.Revisoes);
        var revisao = aluguel.RevisaoAtual;
        var previa = aluguel.CalcularDevolucao(devolucao);
        Assert.AreEqual(EstadoAluguel.EmAberto, aluguel.Estado);
        Assert.IsNull(aluguel.Devolucao);
        Assert.IsNull(aluguel.ExcluidoEmUtc);
        Assert.AreEqual(Agora.AddMinutes(1), aluguel.AlteradoEmUtc);
        Assert.AreEqual(snapshot, JsonSerializer.Serialize(aluguel.Revisoes));
        var final = aluguel.Concluir(devolucao, Agora.AddDays(2));
        Assert.AreEqual(EstadoAluguel.Concluido, aluguel.Estado);
        Assert.AreSame(final, aluguel.Devolucao);
        Assert.AreSame(revisao, aluguel.RevisaoAtual);
        Assert.AreEqual(revisao.Id, final.RevisaoId);
        Assert.AreEqual(aluguel.Id, final.AluguelId);
        Assert.AreEqual(aluguel.EmpresaId, final.EmpresaId);
        Assert.AreEqual(snapshot, JsonSerializer.Serialize(aluguel.Revisoes));
        Assert.AreEqual(previa.TotalFinal, final.TotalFinal);
        Assert.AreEqual(334m, final.TotalFinal);
        Assert.AreEqual(1000m, final.Caucao);
        Assert.AreEqual("devolucao-v1", final.VersaoRegra);
        Assert.AreEqual(Agora.AddDays(2), final.RegistradoEmUtc);
        Assert.IsEmpty(aluguel.RowVersion);
    }

    [TestMethod]
    public void ConcluiUmaVezENaoAdmiteEdicaoPreviaOuOutroFechamento()
    {
        var (aluguel, dados, devolucao) = Cenario();
        var final = aluguel.Concluir(devolucao, Agora.AddDays(2));
        var snapshot = JsonSerializer.Serialize(final);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => aluguel.Concluir(devolucao, Agora.AddDays(3)));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => aluguel.CalcularDevolucao(devolucao));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => aluguel.Editar(dados, Agora.AddDays(3)));
        Assert.AreEqual(snapshot, JsonSerializer.Serialize(aluguel.Devolucao));
        Assert.AreEqual(Agora.AddDays(2), aluguel.AlteradoEmUtc);
        Assert.HasCount(1, aluguel.Revisoes);
    }

    [TestMethod]
    public void OcultaSomenteConcluidoERepeticaoNaoAlteraInstantesOuFinal()
    {
        var (aluguel, _, devolucao) = Cenario();
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => aluguel.Ocultar(Agora.AddMinutes(1)));
        Assert.IsNull(aluguel.ExcluidoEmUtc);
        Assert.AreEqual(Agora, aluguel.AlteradoEmUtc);
        aluguel.Concluir(devolucao, Agora.AddDays(2));
        var contrato = JsonSerializer.Serialize(aluguel.Revisoes);
        var final = JsonSerializer.Serialize(aluguel.Devolucao);
        Assert.IsTrue(aluguel.Ocultar(Agora.AddDays(3)));
        Assert.IsFalse(aluguel.Ocultar(Agora.AddDays(4)));
        Assert.AreEqual(Agora.AddDays(3), aluguel.ExcluidoEmUtc);
        Assert.AreEqual(Agora.AddDays(3), aluguel.AlteradoEmUtc);
        Assert.AreEqual(EstadoAluguel.Concluido, aluguel.Estado);
        Assert.AreEqual(contrato, JsonSerializer.Serialize(aluguel.Revisoes));
        Assert.AreEqual(final, JsonSerializer.Serialize(aluguel.Devolucao));
    }

    [TestMethod]
    public void ValidacaoOuInstanteInvalidoNaoProduzemEfeitoParcial()
    {
        var (aluguel, _, devolucao) = Cenario();
        var snapshot = JsonSerializer.Serialize(aluguel);
        foreach (var invalido in new[]
        {
            devolucao with { RetornoReal = Agora.AddTicks(-1) },
            devolucao with { HodometroFinal = 999 },
            devolucao with { LitrosRestantes = 50.001m },
            devolucao with { TaxasAdicionais = [devolucao.TaxasAdicionais.Single(), devolucao.TaxasAdicionais.Single()] },
        })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => aluguel.Concluir(invalido, Agora.AddDays(2)));
            Assert.AreEqual(snapshot, JsonSerializer.Serialize(aluguel));
        }
        Assert.ThrowsExactly<ArgumentException>(() => aluguel.Concluir(devolucao, Agora.AddTicks(-1)));
        Assert.AreEqual(snapshot, JsonSerializer.Serialize(aluguel));
        aluguel.Concluir(devolucao, Agora.AddDays(2));
        Assert.ThrowsExactly<ArgumentException>(() => aluguel.Ocultar(Agora.AddDays(1)));
        Assert.IsNull(aluguel.ExcluidoEmUtc);
    }

    [TestMethod]
    public void NaoRepeteTaxaContratadaNemAdmiteTaxaInativaOuDeOutraEmpresa()
    {
        var (aluguel, dados, devolucao) = Cenario();
        var snapshot = JsonSerializer.Serialize(aluguel);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            aluguel.Concluir(devolucao with { TaxasAdicionais = dados.Taxas }, Agora.AddDays(2)));
        devolucao.TaxasAdicionais.Single().Excluir(Agora.AddMinutes(1));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => aluguel.Concluir(devolucao, Agora.AddDays(2)));
        var externa = TaxaServico.Nova(Guid.NewGuid(), "Taxa externa", 10, ModalidadeTaxa.Diaria, Agora);
        Assert.ThrowsExactly<EmpresaDivergenteException>(() =>
            aluguel.Concluir(devolucao with { TaxasAdicionais = [externa] }, Agora.AddDays(2)));
        var combustivelExterno = PrecoCombustivel.Novo(Guid.NewGuid(), TipoCombustivel.Gasolina, 6, Agora);
        Assert.ThrowsExactly<EmpresaDivergenteException>(() =>
            aluguel.Concluir(devolucao with { Combustivel = combustivelExterno }, Agora.AddDays(2)));
        Assert.AreEqual(snapshot, JsonSerializer.Serialize(aluguel));
    }

    [TestMethod]
    public void FinalItensEComponentesSaoImutaveisENaoCompartilhamVersaoMutavel()
    {
        var (aluguel, dados, devolucao) = Cenario();
        var versao = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        typeof(EntidadeCadastro).GetProperty(nameof(EntidadeCadastro.RowVersion))!
            .SetValue(devolucao.TaxasAdicionais.Single(), versao);
        var final = aluguel.Concluir(devolucao, Agora.AddDays(2));
        Assert.HasCount(1, final.Itens);
        Assert.AreEqual(OrigemInclusao.Devolucao, final.Itens.Single().OrigemInclusao);
        Assert.IsTrue(final.Componentes.All(c => c.EmpresaId == aluguel.EmpresaId && c.DemonstrativoFinalId == final.Id));
        Assert.ThrowsExactly<NotSupportedException>(() => ((ICollection<ItemDevolucao>)final.Itens).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((ICollection<ComponenteFinal>)final.Componentes).Clear());
        versao[0] = 99;
        var copia = final.Itens.Single().VersaoTaxa;
        copia[1] = 99;
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, final.Itens.Single().VersaoTaxa);
        var snapshot = JsonSerializer.Serialize(final);
        dados.Plano.Alterar(999m, null, null, null, Agora.AddDays(3));
        dados.Seguros.First().Alterar(999m, Agora.AddDays(3));
        devolucao.TaxasAdicionais.Single().Alterar("Taxa reajustada", 999m, ModalidadeTaxa.Fixa, Agora.AddDays(3));
        Assert.AreEqual(snapshot, JsonSerializer.Serialize(final));
    }

    [TestMethod]
    public void VencimentoPosteriorDeCnhNaoBloqueiaRetornoENovosValoresNaoSubstituemContrato()
    {
        var (aluguel, dados, devolucao) = Cenario();
        dados.Veiculo.Alterar(new("FIC1A01", "Outra", "Azul", "Outro", TipoCombustivel.Diesel, 80m, 2026, dados.Grupo.Id), Agora.AddMinutes(1));
        dados.Plano.Alterar(999m, null, null, null, Agora.AddMinutes(1));
        var final = aluguel.Concluir(devolucao with { RetornoReal = Agora.AddDays(5) }, Agora.AddDays(5));
        Assert.AreEqual(50m, final.CapacidadeLitros);
        Assert.AreEqual(TipoCombustivel.Gasolina, final.TipoCombustivel);
        Assert.AreEqual(100m, final.Plano.PrecoDiario);
        Assert.AreEqual(5, final.Diarias);
        Assert.AreEqual(Agora, final.SaidaUtc);
        Assert.AreEqual(Agora.AddDays(2), final.RetornoPrevistoUtc);
    }

    private static (Aluguel Aluguel, DadosContratacao Dados, DadosDevolucao Devolucao) Cenario()
    {
        var empresa = Guid.NewGuid();
        var cliente = Cliente.Novo(empresa, new(TipoCliente.PF, "Cliente sintético", "123.000.000-32",
            null, "RG-SINTETICO", "CNH-SINTETICA", "000000000", new("Estado", "Cidade", "Bairro", "Rua", "S/N"), null), Agora);
        var condutor = Condutor.Novo(empresa, new(cliente.Id, "Condutor sintético", "teste@example.invalid",
            cliente.Cpf!, cliente.Cnh!, new(2026, 11, 26), "000000000"), Agora);
        var grupo = Grupo.Novo(empresa, "Grupo sintético", Agora);
        var veiculo = Veiculo.Novo(empresa, new("FIC1A01", "Marca", "Azul", "Modelo", TipoCombustivel.Gasolina, 50, 2026, grupo.Id), Agora);
        var plano = Plano.Novo(empresa, grupo.Id, ModalidadePlano.Livre, 100, null, null, null, Agora);
        var dados = new DadosContratacao(cliente, condutor, veiculo, grupo, plano, Agora.AddDays(2), 1000,
            [TaxaServico.Nova(empresa, "Taxa fixa", 30, ModalidadeTaxa.Fixa, Agora)],
            [PrecoSeguro.Novo(empresa, TipoSeguro.Cliente, 5, Agora), PrecoSeguro.Novo(empresa, TipoSeguro.Terceiros, 7, Agora)]);
        var devolucao = new DadosDevolucao(Agora.AddDays(2), 1150, 40m,
            PrecoCombustivel.Novo(empresa, TipoCombustivel.Gasolina, 6m, Agora),
            [TaxaServico.Nova(empresa, "Taxa diária", 10, ModalidadeTaxa.Diaria, Agora)]);
        return (Aluguel.Criar(empresa, dados, Agora), dados, devolucao);
    }
}
