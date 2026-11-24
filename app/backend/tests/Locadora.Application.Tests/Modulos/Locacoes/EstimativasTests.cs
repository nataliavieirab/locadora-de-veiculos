using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.Locacoes;

[TestClass]
public sealed class EstimativasTests
{
    [TestMethod]
    public async Task EstimaSemPortaDeEscritaENaoCongelaPrecoDaConfirmacao()
    {
        var consultas = new Consultas();
        var tempo = new Relogio();
        var casos = new CasosEstimativas(consultas, new Contexto(consultas.Dados.Cliente.EmpresaId), tempo);
        var previa = await casos.EstimarAsync(consultas.Comando, null, null, CancellationToken.None);
        Assert.AreEqual(200m, previa.Revisao.Estimativa.TotalConhecido);
        Assert.AreEqual(tempo.Agora, previa.SaidaUtc);
        consultas.Dados.Plano.Alterar(120, null, null, null, tempo.Agora);
        tempo.Agora = tempo.Agora.AddMinutes(1);
        var confirmada = Aluguel.Criar(consultas.Dados.Cliente.EmpresaId, consultas.Dados, tempo.GetUtcNow());
        Assert.AreEqual(240m, confirmada.RevisaoAtual.Estimativa.TotalConhecido);
        Assert.AreNotEqual(previa.SaidaUtc, confirmada.SaidaUtc);
        Assert.AreEqual(200m, previa.Revisao.Estimativa.TotalConhecido);
    }

    [TestMethod]
    public async Task IndisponibilidadeNaoEntregaPreviaEComandoDuplicadoNaoConsulta()
    {
        var consultas = new Consultas { Ocupado = true };
        var casos = new CasosEstimativas(consultas, new Contexto(consultas.Dados.Cliente.EmpresaId), new Relogio());
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.EstimarAsync(consultas.Comando, null, null, CancellationToken.None));
        var chamadas = consultas.Chamadas;
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() => casos.EstimarAsync(
            consultas.Comando with { TaxasServicoIds = [Guid.Empty, Guid.Empty] }, null, null, CancellationToken.None));
        Assert.AreEqual(chamadas, consultas.Chamadas);
    }

    [TestMethod]
    public async Task ContextoDeEdicaoExternoOuInexistenteNaoVazaDados()
    {
        var consultas = new Consultas();
        var casos = new CasosEstimativas(consultas, new Contexto(consultas.Dados.Cliente.EmpresaId), new Relogio());
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            casos.EstimarAsync(consultas.Comando, Guid.NewGuid(), null, CancellationToken.None));
        Assert.AreEqual(0, consultas.Chamadas);
    }

    internal sealed class Relogio : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 11, 23, 13, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed record Contexto(Guid EmpresaId) : IContextoCadastro;

    internal sealed class Consultas : IConsultasContratacao
    {
        public DadosContratacao Dados { get; }
        public ComandoContratacao Comando { get; }
        public bool Ocupado { get; set; }
        public int Chamadas { get; private set; }

        public Consultas()
        {
            var empresa = Guid.NewGuid();
            var agora = new Relogio().Agora;
            var cliente = Cliente.Novo(empresa, new(TipoCliente.PF, "Cliente sintético", "12300000032", null,
                "RG", "CNH-SINTETICA", "000", new("Estado", "Cidade", "Bairro", "Rua", "S/N"), null), agora);
            var condutor = Condutor.Novo(empresa, new(cliente.Id, "Condutor", "teste@example.invalid",
                cliente.Cpf!, cliente.Cnh!, new(2028, 1, 1), "000"), agora);
            var grupo = Grupo.Novo(empresa, "Grupo", agora);
            var veiculo = Veiculo.Novo(empresa, new("FIC1A01", "Marca", "Azul", "Modelo", TipoCombustivel.Gasolina, 50, 2026, grupo.Id), agora);
            var plano = Plano.Novo(empresa, grupo.Id, ModalidadePlano.Livre, 100, null, null, null, agora);
            Dados = new(cliente, condutor, veiculo, grupo, plano, agora.AddDays(2), 0, [], []);
            Comando = new(cliente.Id, condutor.Id, veiculo.Id, plano.Id, Dados.RetornoPrevisto, 0, [], []);
        }
        public Task<DadosContratacao> CarregarAsync(ComandoContratacao comando, CancellationToken cancelamento)
        {
            Chamadas++;
            return Task.FromResult(Dados);
        }
        public Task<Aluguel?> AluguelAsync(Guid id, CancellationToken cancelamento) => Task.FromResult<Aluguel?>(null);
        public Task<bool> VeiculoOcupadoAsync(Guid veiculoId, Guid? excetoAluguelId, CancellationToken cancelamento) => Task.FromResult(Ocupado);
        public Task<Pagina<Veiculo>> VeiculosAsync(Paginacao paginacao, Guid? grupoId, bool somenteDisponiveis, Guid? aluguelEmEdicaoId, CancellationToken cancelamento) => throw new NotSupportedException();
        public Task<Pagina<Condutor>> CondutoresAsync(Paginacao paginacao, Guid clienteId, CancellationToken cancelamento) => throw new NotSupportedException();
    }
}
