using System.Text.Json;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.Locacoes;

[TestClass]
public sealed class PreviaDevolucaoTests
{
    [TestMethod]
    public async Task PreviaSoConsultaPreservaRaizERecalculoExigeMesmaIntencao()
    {
        var consultas = new Consultas();
        var casos = consultas.Casos();
        var original = JsonSerializer.Serialize(consultas.Aluguel);
        var previa = await casos.PreviaAsync(consultas.Aluguel.Id, consultas.Comando, consultas.Etag, CancellationToken.None);
        Assert.AreEqual(280m, previa.Calculo.TotalFinal);
        Assert.AreEqual(original, JsonSerializer.Serialize(consultas.Aluguel));
        Assert.IsTrue(consultas.Releu);
        await casos.RecalcularAceiteAsync(consultas.Aluguel, new(consultas.Comando, previa.VersoesAceitas), CancellationToken.None);
        Assert.IsTrue(consultas.Confirmando);
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => casos.RecalcularAceiteAsync(
            consultas.Aluguel, new(consultas.Comando with { LitrosRestantes = 39m }, previa.VersoesAceitas), CancellationToken.None));
        Assert.AreEqual("previa_desatualizada", erro.Codigo);
        Assert.AreEqual(original, JsonSerializer.Serialize(consultas.Aluguel));
    }

    [TestMethod]
    public async Task ExigeVersaoENaoAceitaLeituraObsoletaOuEmpresaExterna()
    {
        var consultas = new Consultas();
        var casos = consultas.Casos();
        var ausente = await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.PreviaAsync(consultas.Aluguel.Id, consultas.Comando, null, CancellationToken.None));
        Assert.IsTrue(ausente.Ausente);
        consultas.Mudou = true;
        var divergente = await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.PreviaAsync(consultas.Aluguel.Id, consultas.Comando, consultas.Etag, CancellationToken.None));
        Assert.IsFalse(divergente.Ausente);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            consultas.Casos(Guid.NewGuid()).PreviaAsync(consultas.Aluguel.Id, consultas.Comando, consultas.Etag, CancellationToken.None));
    }

    [TestMethod]
    public async Task HashNormalizaOffsetEscalaEOrdemSemUsarInstanteDeConsulta()
    {
        var consultas = new Consultas();
        var casos = consultas.Casos();
        var primeira = await casos.PreviaAsync(consultas.Aluguel.Id, consultas.Comando, consultas.Etag, CancellationToken.None);
        consultas.Taxas.Reverse();
        var segunda = await casos.PreviaAsync(consultas.Aluguel.Id, consultas.Comando with
        {
            RetornoReal = consultas.Comando.RetornoReal.ToOffset(TimeSpan.FromHours(-3)),
            LitrosRestantes = 40.000m, TaxasAdicionaisIds = consultas.Comando.TaxasAdicionaisIds.Reverse().ToArray(),
        }, consultas.Etag, CancellationToken.None);
        Assert.AreEqual(primeira.VersoesAceitas.ResumoCalculo, segunda.VersoesAceitas.ResumoCalculo);
        AceiteDevolucao.Exigir(primeira.VersoesAceitas with
        {
            TaxasNovas = primeira.VersoesAceitas.TaxasNovas.Reverse().ToArray(),
        }, segunda.VersoesAceitas);
        var comOutroAtor = await consultas.Casos(conta: Guid.NewGuid()).PreviaAsync(
            consultas.Aluguel.Id, consultas.Comando, consultas.Etag, CancellationToken.None);
        Assert.AreNotEqual(primeira.VersoesAceitas.ResumoCalculo, comOutroAtor.VersoesAceitas.ResumoCalculo);
    }

    [TestMethod]
    public async Task PrecoVersaoRegraEHashAdulteradosNaoSaoAceitos()
    {
        var consultas = new Consultas();
        var casos = consultas.Casos();
        var previa = await casos.PreviaAsync(consultas.Aluguel.Id, consultas.Comando, consultas.Etag, CancellationToken.None);
        foreach (var alterado in new[]
        {
            previa.VersoesAceitas with { RevisaoId = Guid.NewGuid() },
            previa.VersoesAceitas with { CombustivelId = Guid.NewGuid() },
            previa.VersoesAceitas with { VersaoRegra = "outra-regra" },
            previa.VersoesAceitas with { ResumoCalculo = new string('0', 64) },
            previa.VersoesAceitas with { TaxasNovas = [] },
        })
            Assert.ThrowsExactly<ConflitoCadastroException>(() => AceiteDevolucao.Exigir(alterado, previa.VersoesAceitas));
        consultas.Taxas[0].Alterar("Preço novo", 99m, ModalidadeTaxa.Diaria, consultas.Comando.RetornoReal);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => casos.RecalcularAceiteAsync(
            consultas.Aluguel, new(consultas.Comando, previa.VersoesAceitas), CancellationToken.None));
    }

    [TestMethod]
    public void FormaDeAceiteInvalidaNaoETratadaComoAutorizacao()
    {
        var taxa = new VersaoTaxaAceita(Guid.NewGuid(), "\"versao\"");
        var valido = new VersoesDevolucaoAceitas(Guid.NewGuid(), "devolucao-v1", Guid.NewGuid(), [taxa], new string('A', 64));
        foreach (var invalido in new[]
        {
            valido with { RevisaoId = Guid.Empty }, valido with { CombustivelId = Guid.Empty },
            valido with { VersaoRegra = "" }, valido with { ResumoCalculo = "invalido" },
            valido with { TaxasNovas = [taxa, taxa] }, valido with { TaxasNovas = [null!] },
        })
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => AceiteDevolucao.Validar(invalido));
    }

    private sealed record Contexto(Guid EmpresaId, Guid ContaId) : IContextoLocacao;

    private sealed class Consultas : IConsultasDevolucao
    {
        private readonly Guid _conta = Guid.NewGuid();
        public Aluguel Aluguel { get; }
        public List<TaxaServico> Taxas { get; }
        public ComandoDevolucao Comando { get; }
        public string Etag => VersaoCadastro.Etag(Aluguel.RowVersion);
        public bool Mudou { get; set; }
        public bool Releu { get; private set; }
        public bool Confirmando { get; private set; }

        public Consultas()
        {
            var dados = new EstimativasTests.Consultas().Dados;
            var agora = new EstimativasTests.Relogio().Agora;
            Aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, agora);
            typeof(EntidadeCadastro).GetProperty(nameof(EntidadeCadastro.RowVersion))!.SetValue(Aluguel, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
            Taxas = [TaxaServico.Nova(Aluguel.EmpresaId, "Taxa A", 5m, ModalidadeTaxa.Diaria, agora),
                TaxaServico.Nova(Aluguel.EmpresaId, "Taxa B", 10m, ModalidadeTaxa.Fixa, agora)];
            foreach (var taxa in Taxas)
                typeof(EntidadeCadastro).GetProperty(nameof(EntidadeCadastro.RowVersion))!.SetValue(taxa, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 });
            Comando = new(agora.AddDays(2), 150, 40m, Taxas.Select(t => t.Id).ToArray());
        }

        public PreviaDevolucao Casos(Guid? empresa = null, Guid? conta = null)
            => new(this, new Contexto(empresa ?? Aluguel.EmpresaId, conta ?? _conta));

        public Task<Aluguel?> AluguelAsync(Guid id, CancellationToken cancelamento)
            => Task.FromResult<Aluguel?>(id == Aluguel.Id ? Aluguel : null);

        private PrecoCombustivel? _combustivel;
        public Task<DadosDevolucao> CarregarAsync(Aluguel aluguel, ComandoDevolucao comando,
            CancellationToken cancelamento, bool confirmarAceite = false)
        {
            Confirmando = confirmarAceite;
            _combustivel ??= PrecoCombustivel.Novo(Aluguel.EmpresaId, TipoCombustivel.Gasolina, 6m, Aluguel.SaidaUtc);
            return Task.FromResult(new DadosDevolucao(comando.RetornoReal, comando.HodometroFinal,
                comando.LitrosRestantes, _combustivel, Taxas));
        }

        public Task<byte[]?> VersaoAluguelAsync(Guid id, CancellationToken cancelamento)
        {
            Releu = true;
            return Task.FromResult<byte[]?>(Mudou ? [0, 0, 0, 0, 0, 0, 0, 9] : Aluguel.RowVersion.ToArray());
        }
    }
}
