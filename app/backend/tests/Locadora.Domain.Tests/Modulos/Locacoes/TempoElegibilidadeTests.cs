using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.Locacoes;

[TestClass]
public sealed class TempoElegibilidadeTests
{
    private static readonly DateTimeOffset Saida = new(2026, 11, 23, 13, 0, 0, TimeSpan.Zero);
    private static DadosCliente Pf => new(TipoCliente.PF, "Cliente sintético", "123.000.000-32", null,
        "RG-SINTETICO", "CNH-SINTETICA", "000000000",
        new("Estado", "Cidade", "Bairro", "Rua", "S/N"), null);

    [TestMethod]
    public void DiariasUsamTicksTranscorridosSemToleranciaNemViradaCivil()
    {
        Assert.AreEqual(1, TempoLocacao.ContarDiarias(Saida, Saida));
        Assert.AreEqual(1, TempoLocacao.ContarDiarias(Saida, Saida.AddHours(24)));
        Assert.AreEqual(2, TempoLocacao.ContarDiarias(Saida, Saida.AddHours(24).AddMinutes(1)));
        Assert.AreEqual(2, TempoLocacao.ContarDiarias(Saida, Saida.AddHours(24).AddTicks(1)));
        Assert.AreEqual(1, TempoLocacao.ContarDiarias(Saida, Saida.AddHours(24).ToOffset(TimeSpan.FromHours(9))));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => TempoLocacao.ContarDiarias(Saida, Saida.AddTicks(-1)));
    }

    [TestMethod]
    public void TransicaoHistoricaNaoContaDiaCivilComoVinteQuatroHoras()
    {
        var inicio = DateTimeOffset.Parse("2018-11-03T12:00:00-03:00");
        var fim = DateTimeOffset.Parse("2018-11-04T12:00:00-02:00");
        Assert.AreEqual(1, TempoLocacao.ContarDiarias(inicio, fim));
        var inicioFimVerao = DateTimeOffset.Parse("2019-02-16T12:00:00-02:00");
        var fimVerao = DateTimeOffset.Parse("2019-02-17T12:00:00-03:00");
        Assert.AreEqual(2, TempoLocacao.ContarDiarias(inicioFimVerao, fimVerao));
    }

    [TestMethod]
    public void HorarioLocalExigeOffsetRealEAceitaDesambiguacaoExplicita()
    {
        Assert.AreEqual(Saida, TempoLocacao.NormalizarHorarioSaoPaulo(Saida.ToOffset(TimeSpan.FromHours(-3))));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            TempoLocacao.NormalizarHorarioSaoPaulo(DateTimeOffset.Parse("2018-11-04T00:30:00-03:00")));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            TempoLocacao.NormalizarHorarioSaoPaulo(DateTimeOffset.Parse("2026-11-23T10:00:00-02:00")));
        var primeira = TempoLocacao.NormalizarHorarioSaoPaulo(DateTimeOffset.Parse("2019-02-16T23:30:00-02:00"));
        var segunda = TempoLocacao.NormalizarHorarioSaoPaulo(DateTimeOffset.Parse("2019-02-16T23:30:00-03:00"));
        Assert.AreEqual(TimeSpan.FromHours(1), segunda - primeira);
    }

    [TestMethod]
    public void ValidadeIncluiDiaLocalENaoDiaUtcOuFusoDoDispositivo()
    {
        var retorno = DateTimeOffset.Parse("2026-11-25T02:30:00Z");
        Assert.AreEqual(new DateOnly(2026, 11, 24), TempoLocacao.DiaSaoPaulo(retorno));
        foreach (var offset in new[] { -8, 0, 9 })
        {
            ElegibilidadeLocacao.ValidarDocumentacao(TipoCliente.PF, Pf.Cpf, null, Pf.Cnh,
                Pf.Cpf, Pf.Cnh, new(2026, 11, 24), retorno.ToOffset(TimeSpan.FromHours(offset)));
        }
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            ElegibilidadeLocacao.ValidarDocumentacao(TipoCliente.PF, Pf.Cpf, null, Pf.Cnh,
                Pf.Cpf, Pf.Cnh, new(2026, 11, 23), retorno));
    }

    [TestMethod]
    public void DocumentacaoInvalidaOuAusenteECnhIncoerenteNaoSaoElegiveis()
    {
        var retorno = Saida.AddDays(2);
        var validade = new DateOnly(2028, 1, 1);
        foreach (var cpf in new[] { null, "", "11111111111", "12300000033" })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
                ElegibilidadeLocacao.ValidarDocumentacao(TipoCliente.PF, Pf.Cpf, null, Pf.Cnh,
                    cpf, Pf.Cnh, validade, retorno));
            Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
                ElegibilidadeLocacao.ValidarDocumentacao(TipoCliente.PF, cpf, null, Pf.Cnh,
                    Pf.Cpf, Pf.Cnh, validade, retorno));
        }
        foreach (var cnh in new[] { null, "", "!!!" })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
                ElegibilidadeLocacao.ValidarDocumentacao(TipoCliente.PF, Pf.Cpf, null, Pf.Cnh,
                    Pf.Cpf, cnh, validade, retorno));
        }
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            ElegibilidadeLocacao.ValidarDocumentacao(TipoCliente.PF, Pf.Cpf, null, "OUTRA-CNH",
                Pf.Cpf, Pf.Cnh, validade, retorno));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            ElegibilidadeLocacao.ValidarDocumentacao(TipoCliente.PF, Pf.Cpf, null, Pf.Cnh,
                Pf.Cpf, Pf.Cnh, default, retorno));
        foreach (var cnpj in new[] { null, "", "00000000000000", "98700000000108" })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
                ElegibilidadeLocacao.ValidarDocumentacao(TipoCliente.PJ, null, cnpj, null,
                    Pf.Cpf, Pf.Cnh, validade, retorno));
        }
    }

    [TestMethod]
    public void PfProprioCondutorEPjComCondutorVinculadoSaoElegiveis()
    {
        foreach (var tipo in new[] { TipoCliente.PF, TipoCliente.PJ })
        {
            var (cliente, condutor, veiculo, grupo, plano) = Referencias(tipo);
            ElegibilidadeLocacao.ValidarContratacao(cliente, condutor, veiculo, grupo, plano, Saida, Saida.AddDays(2), 0);
        }
    }

    [TestMethod]
    public void RejeitaVinculoAlheioPlanoIncompativelETenantCruzado()
    {
        var (cliente, condutor, veiculo, grupo, plano) = Referencias();
        var outroCliente = Cliente.Novo(cliente.EmpresaId, Pf, Saida);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            ElegibilidadeLocacao.ValidarContratacao(outroCliente, condutor, veiculo, grupo, plano, Saida, Saida.AddDays(2), 0));
        var outroPlano = Plano.Novo(cliente.EmpresaId, Guid.NewGuid(), ModalidadePlano.Livre, 100, null, null, null, Saida);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            ElegibilidadeLocacao.ValidarContratacao(cliente, condutor, veiculo, grupo, outroPlano, Saida, Saida.AddDays(2), 0));
        var planoExterno = Plano.Novo(Guid.NewGuid(), grupo.Id, ModalidadePlano.Livre, 100, null, null, null, Saida);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            ElegibilidadeLocacao.ValidarContratacao(cliente, condutor, veiculo, grupo, planoExterno, Saida, Saida.AddDays(2), 0));
    }

    [TestMethod]
    public void ReferenciasInativasRetornoNaoPosteriorEHodometroNegativoSaoRejeitados()
    {
        for (var indice = 0; indice < 5; indice++)
        {
            var (cliente, condutor, veiculo, grupo, plano) = Referencias();
            CadastroExcluivel[] referencias = [cliente, condutor, veiculo, grupo, plano];
            referencias[indice].Excluir(Saida);
            Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
                ElegibilidadeLocacao.ValidarContratacao(cliente, condutor, veiculo, grupo, plano, Saida, Saida.AddDays(2), 0));
        }
        var (c, d, v, g, p) = Referencias();
        foreach (var retorno in new[] { Saida, Saida.AddTicks(-1) })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
                ElegibilidadeLocacao.ValidarContratacao(c, d, v, g, p, Saida, retorno, 0));
        }
        Assert.ThrowsExactly<ValidacaoCadastroException>(() =>
            ElegibilidadeLocacao.ValidarContratacao(c, d, v, g, p, Saida, Saida.AddDays(2), -1));
    }

    private static (Cliente, Condutor, Veiculo, Grupo, Plano) Referencias(TipoCliente tipo = TipoCliente.PF)
    {
        var empresa = Guid.NewGuid();
        var dados = tipo == TipoCliente.PF ? Pf : Pf with
        {
            Tipo = TipoCliente.PJ, Cpf = null, Rg = null, Cnh = null, Cnpj = "98.700.000/0001-07",
        };
        var cliente = Cliente.Novo(empresa, dados, Saida);
        var condutor = Condutor.Novo(empresa, new(cliente.Id, "Condutor sintético", "teste@example.invalid",
            Pf.Cpf!, Pf.Cnh!, new(2028, 1, 1), "000000000"), Saida);
        var grupo = Grupo.Novo(empresa, "Grupo sintético", Saida);
        var plano = Plano.Novo(empresa, grupo.Id, ModalidadePlano.Livre, 100, null, null, null, Saida);
        var veiculo = Veiculo.Novo(empresa, new("FIC1A01", "Marca", "Azul", "Modelo",
            TipoCombustivel.Gasolina, 50, 2026, grupo.Id), Saida);
        return (cliente, condutor, veiculo, grupo, plano);
    }
}
