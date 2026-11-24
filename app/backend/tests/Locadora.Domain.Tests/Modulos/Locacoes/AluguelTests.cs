using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.Locacoes;

[TestClass]
public sealed class AluguelTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 13, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void CriacaoCapturaContratoCheioEstimativaEIdentidadesDaEmpresa()
    {
        var dados = Dados();
        var aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, Agora.ToOffset(TimeSpan.FromHours(-3)));
        var revisao = aluguel.RevisaoAtual;
        Assert.AreEqual(Agora, aluguel.SaidaUtc);
        Assert.AreEqual(TimeSpan.Zero, aluguel.SaidaUtc.Offset);
        Assert.AreEqual(EstadoAluguel.EmAberto, aluguel.Estado);
        Assert.AreEqual(1, aluguel.NumeroRevisaoAtual);
        Assert.AreEqual(dados.Cliente.Id, revisao.ClienteId);
        Assert.AreEqual(dados.Condutor.Id, revisao.CondutorId);
        Assert.AreEqual(dados.Veiculo.Id, revisao.VeiculoId);
        Assert.AreEqual(dados.Plano.Id, revisao.PlanoId);
        Assert.AreEqual(50m, revisao.Veiculo.CapacidadeLitros);
        Assert.AreEqual(1000m, revisao.Caucao);
        Assert.AreEqual(244m, revisao.Estimativa.TotalConhecido);
        Assert.IsNull(revisao.Estimativa.Quilometros);
        Assert.IsTrue(revisao.Estimativa.QuilometragemPendente);
        Assert.IsTrue(revisao.Itens.All(item => item.EmpresaId == aluguel.EmpresaId && item.RevisaoId == revisao.Id
            && item.OrigemInclusao == OrigemInclusao.Contratacao && item.RevisaoPrimeiraInclusao == 1));
    }

    [TestMethod]
    public void AlterarCadastrosNaoReescreveSnapshotNemEstimativa()
    {
        var dados = Dados();
        var aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, Agora);
        var revisao = aluguel.RevisaoAtual;
        var nomeOriginal = dados.Cliente.Nome;
        var nomeGrupo = dados.Grupo.Nome;
        dados.Cliente.Alterar(new(TipoCliente.PF, "Nome alterado", dados.Cliente.Cpf, null,
            dados.Cliente.Rg, dados.Cliente.Cnh, "999", new("Outro estado", "Outra cidade", "Outro bairro", "Outra rua", "42"), null), Agora.AddMinutes(1));
        dados.Grupo.Alterar("Grupo alterado", Agora.AddMinutes(1));
        dados.Plano.Alterar(300, null, 200, 5, Agora.AddMinutes(1));
        dados.Seguros.Single().Alterar(99, Agora.AddMinutes(1));
        Assert.AreEqual(nomeOriginal, revisao.Cliente.Nome);
        Assert.AreEqual("Rua", revisao.Cliente.Endereco.Rua);
        Assert.AreEqual(nomeGrupo, revisao.Veiculo.GrupoNome);
        Assert.AreEqual(100m, revisao.Plano.PrecoDiario);
        Assert.AreEqual(7m, revisao.Itens.Single(i => i.Categoria == CategoriaItem.Seguro).PrecoUnitario);
        Assert.AreEqual(244m, revisao.Estimativa.TotalConhecido);
    }

    [TestMethod]
    public void EdicaoMantemSaidaPrecoEHistoricoERecalculaPrazo()
    {
        var dados = Dados();
        var aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, Agora);
        var primeira = aluguel.RevisaoAtual;
        dados.Plano.Alterar(9999999999999m, null, 200, 5, Agora.AddMinutes(1));
        dados.Seguros.Single().Alterar(99, Agora.AddMinutes(1));
        var edicao = dados with { RetornoPrevisto = Agora.AddDays(3) };
        aluguel.Editar(edicao, Agora.AddMinutes(2));
        Assert.AreEqual(Agora, aluguel.SaidaUtc);
        Assert.AreEqual(2, aluguel.NumeroRevisaoAtual);
        Assert.AreEqual(351m, aluguel.RevisaoAtual.Estimativa.TotalConhecido);
        Assert.AreEqual(100m, aluguel.RevisaoAtual.Plano.PrecoDiario);
        Assert.AreEqual(244m, primeira.Estimativa.TotalConhecido);
        Assert.AreEqual(Agora.AddDays(2), primeira.RetornoPrevistoUtc);
        Assert.HasCount(2, aluguel.Revisoes);
        Assert.AreNotEqual(primeira.Id, aluguel.RevisaoAtual.Id);
        Assert.AreNotEqual(primeira.Itens.First().Id, aluguel.RevisaoAtual.Itens.First().Id);
    }

    [TestMethod]
    public void PreviaContextualNaoMudaRaizOuColecoes()
    {
        var dados = Dados();
        var aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, Agora);
        var primeira = aluguel.RevisaoAtual;
        var previa = aluguel.PrepararEdicao(dados with { RetornoPrevisto = Agora.AddDays(3) }, Agora.AddMinutes(1));
        Assert.AreEqual(2, previa.Numero);
        Assert.AreEqual(351m, previa.Estimativa.TotalConhecido);
        Assert.AreSame(primeira, aluguel.RevisaoAtual);
        Assert.AreEqual(1, aluguel.NumeroRevisaoAtual);
        Assert.AreEqual(Agora, aluguel.AlteradoEmUtc);
        Assert.HasCount(1, aluguel.Revisoes);
    }

    [TestMethod]
    public void RemoverEReincluirSeguroUsaPrecoAtualEOrigemNovaSemAlterarAnterior()
    {
        var dados = Dados();
        var aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, Agora);
        var primeira = aluguel.RevisaoAtual;
        aluguel.Editar(dados with { Seguros = [] }, Agora.AddMinutes(1));
        dados.Seguros.Single().Alterar(19, Agora.AddMinutes(2));
        aluguel.Editar(dados, Agora.AddMinutes(3));
        var item = aluguel.RevisaoAtual.Itens.Single(i => i.Categoria == CategoriaItem.Seguro);
        Assert.AreEqual(19m, item.PrecoUnitario);
        Assert.AreEqual(OrigemInclusao.Edicao, item.OrigemInclusao);
        Assert.AreEqual(3, item.RevisaoPrimeiraInclusao);
        Assert.AreEqual(7m, primeira.Itens.Single(i => i.Categoria == CategoriaItem.Seguro).PrecoUnitario);
        Assert.AreEqual(268m, aluguel.RevisaoAtual.Estimativa.TotalConhecido);
        Assert.AreEqual(244m, primeira.Estimativa.TotalConhecido);
        Assert.HasCount(1, aluguel.Revisoes.Single(r => r.Numero == 2).Itens);
    }

    [TestMethod]
    public void SubstituirVeiculoPlanoEAdicionarItemCapturaNovosSemTransferirIdentidade()
    {
        var dados = Dados();
        var aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, Agora);
        var primeira = aluguel.RevisaoAtual;
        var empresa = aluguel.EmpresaId;
        var grupo = Grupo.Novo(empresa, "Outro grupo", Agora);
        var veiculo = Veiculo.Novo(empresa, new(dados.Veiculo.Placa, "Outra marca", "Azul", "Outro modelo",
            TipoCombustivel.Diesel, 60, 2026, grupo.Id), Agora);
        var plano = Plano.Novo(empresa, grupo.Id, ModalidadePlano.Controlado, 200, null, 10, 9, Agora);
        var taxa = TaxaServico.Nova(empresa, "Taxa nova", 10, ModalidadeTaxa.Diaria, Agora);
        aluguel.Editar(dados with { Veiculo = veiculo, Grupo = grupo, Plano = plano, Taxas = [.. dados.Taxas, taxa] }, Agora.AddMinutes(1));
        Assert.AreEqual(veiculo.Id, aluguel.VeiculoId);
        Assert.AreEqual(plano.Id, aluguel.PlanoId);
        Assert.AreEqual(200m, aluguel.RevisaoAtual.Plano.PrecoDiario);
        Assert.AreEqual(dados.Veiculo.Id, primeira.VeiculoId);
        Assert.AreNotEqual(primeira.VeiculoId, aluguel.VeiculoId);
        Assert.AreEqual("Outro modelo", aluguel.RevisaoAtual.Veiculo.Modelo);
        Assert.AreEqual(dados.Veiculo.Modelo, primeira.Veiculo.Modelo);
        var novo = aluguel.RevisaoAtual.Itens.Single(i => i.OrigemId == taxa.Id);
        Assert.AreEqual(2, novo.RevisaoPrimeiraInclusao);
        Assert.AreEqual(OrigemInclusao.Edicao, novo.OrigemInclusao);
        Assert.AreEqual(464m, aluguel.RevisaoAtual.Estimativa.TotalConhecido);
    }

    [TestMethod]
    public void AjusteSomenteDeItensGeraRevisaoERejeicaoNaoMudaRaiz()
    {
        var dados = Dados();
        var aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, Agora);
        aluguel.Editar(dados with { Taxas = [] }, Agora.AddMinutes(1));
        Assert.AreEqual(2, aluguel.NumeroRevisaoAtual);
        var segunda = aluguel.RevisaoAtual;
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            aluguel.Editar(dados with { RetornoPrevisto = Agora }, Agora.AddMinutes(2)));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            aluguel.Editar(dados with { Taxas = [dados.Taxas.Single(), dados.Taxas.Single()] }, Agora.AddMinutes(2)));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            aluguel.Editar(dados with { Seguros = [PrecoSeguro.Novo(Guid.NewGuid(), TipoSeguro.Cliente, 0, Agora)] }, Agora.AddMinutes(2)));
        Assert.AreSame(segunda, aluguel.RevisaoAtual);
        Assert.AreEqual(Agora.AddMinutes(1), aluguel.AlteradoEmUtc);
        Assert.HasCount(2, aluguel.Revisoes);
    }

    [TestMethod]
    public void ColecoesNaoSaoMutaveisEReferenciasDeOutraEmpresaSaoNegadas()
    {
        var dados = Dados();
        var aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, Agora);
        Assert.ThrowsExactly<NotSupportedException>(() => ((ICollection<RevisaoContrato>)aluguel.Revisoes).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((ICollection<ItemContratado>)aluguel.RevisaoAtual.Itens).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((ICollection<ComponenteEstimativa>)aluguel.RevisaoAtual.Estimativa.Componentes).Clear());
        Assert.ThrowsExactly<EmpresaDivergenteException>(() => Aluguel.Criar(Guid.NewGuid(), dados, Agora));
        dados.Taxas.Single().Excluir(Agora.AddMinutes(1));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => aluguel.Editar(dados, Agora.AddMinutes(2)));
    }

    private static DadosContratacao Dados()
    {
        var empresa = Guid.NewGuid();
        var cliente = Cliente.Novo(empresa, new(TipoCliente.PF, "Cliente sintético", "123.000.000-32",
            null, "RG-SINTETICO", "CNH-SINTETICA", "000000000", new("Estado", "Cidade", "Bairro", "Rua", "S/N"), null), Agora);
        var condutor = Condutor.Novo(empresa, new(cliente.Id, "Condutor sintético", "teste@example.invalid",
            cliente.Cpf!, cliente.Cnh!, new(2028, 1, 1), "000000000"), Agora);
        var grupo = Grupo.Novo(empresa, "Grupo sintético", Agora);
        var veiculo = Veiculo.Novo(empresa, new("FIC1A01", "Marca", "Azul", "Modelo", TipoCombustivel.Gasolina, 50, 2026, grupo.Id), Agora);
        var plano = Plano.Novo(empresa, grupo.Id, ModalidadePlano.Controlado, 100, null, 100, 2, Agora);
        return new(cliente, condutor, veiculo, grupo, plano, Agora.AddDays(2), 1000,
            [TaxaServico.Nova(empresa, "Taxa sintética", 30, ModalidadeTaxa.Fixa, Agora)],
            [PrecoSeguro.Novo(empresa, TipoSeguro.Terceiros, 7, Agora)]);
    }
}
