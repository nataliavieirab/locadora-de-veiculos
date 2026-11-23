using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.FrotaPrecos;

[TestClass]
public sealed class PrecosTransacoesSqlTests
{
    private static readonly CancellationToken Token = CancellationToken.None;
    private static string Versao(EntidadeCadastro item) => VersaoCadastro.Etag(item.RowVersion);

    [TestMethod]
    public async Task TaxasPaginamEditamSemMudancaArtificialEExcluemLogicamente()
    {
        await using var a = await Ambiente.CriarAsync();
        var primeira = await a.Taxas().CriarAsync(new("Assento sintético", 5m, ModalidadeTaxa.Diaria), Token);
        var segunda = await a.Taxas().CriarAsync(new("Limpeza sintética", 0m, ModalidadeTaxa.Fixa), Token);
        var pagina = await a.Taxas().ListarAsync(new Paginacao(2, 1), Token);
        Assert.AreEqual(2, pagina.Total);
        Assert.AreEqual(segunda.Id, pagina.Items.Single().Id);
        var capturado = primeira.Preco;
        var atual = await a.Taxas().AlterarAsync(primeira.Id, new("Assento sintético", 7.123456m, ModalidadeTaxa.Diaria), Versao(primeira), Token);
        Assert.AreEqual(5m, capturado);
        Assert.AreEqual(7.123456m, atual.Preco);
        Assert.IsFalse(primeira.RowVersion.SequenceEqual(atual.RowVersion));
        Assert.AreEqual("preco", (await a.Banco.AuditoriasCadastro.SingleAsync(item => item.Acao == AcaoCadastro.Alteracao)).CamposAlterados);
        var repetida = await a.Taxas().AlterarAsync(atual.Id, new("Assento sintético", 7.123456m, ModalidadeTaxa.Diaria), Versao(atual), Token);
        CollectionAssert.AreEqual(atual.RowVersion, repetida.RowVersion);
        Assert.AreEqual(3, await a.Banco.AuditoriasCadastro.CountAsync());
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            a.Taxas().AlterarAsync(atual.Id, new("Outro", 1m, ModalidadeTaxa.Fixa), null, Token));
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            a.Taxas().ExcluirAsync(atual.Id, Versao(primeira), Token));
        await a.Taxas().ExcluirAsync(atual.Id, Versao(atual), Token);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Taxas().ConsultarAsync(atual.Id, Token));
        var historico = await a.Banco.TaxasServicos.IgnoreQueryFilters()
            .SingleAsync(item => item.EmpresaId == a.Contexto.EmpresaId && item.Id == atual.Id);
        Assert.IsNotNull(historico.ExcluidoEmUtc);
        Assert.AreEqual(7.123456m, historico.Preco);
        Assert.AreEqual(1, (await a.Taxas().ListarAsync(new Paginacao(), Token)).Total);
    }

    [TestMethod]
    public async Task TaxasIsolamLeituraEEscritaEntreEmpresas()
    {
        await using var a = await Ambiente.CriarAsync();
        await using var b = await Ambiente.CriarAsync();
        var taxaA = await a.Taxas().CriarAsync(new("Taxa", 5m, ModalidadeTaxa.Fixa), Token);
        var taxaB = await b.Taxas().CriarAsync(new("Taxa", 9m, ModalidadeTaxa.Diaria), Token);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Taxas().ConsultarAsync(taxaB.Id, Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            a.Taxas().AlterarAsync(taxaB.Id, new("Outro", 1m, ModalidadeTaxa.Fixa), Versao(taxaB), Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Taxas().ExcluirAsync(taxaB.Id, Versao(taxaB), Token));
        Assert.AreEqual(taxaA.Id, (await a.Taxas().ListarAsync(new Paginacao(), Token)).Items.Single().Id);
        Assert.AreEqual(9m, (await b.Taxas().ConsultarAsync(taxaB.Id, Token)).Preco);
        Assert.AreEqual(1, await a.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task PortaControladaBloqueiaTaxaSemEfeitoOuAuditoriaParcial()
    {
        await using var a = await Ambiente.CriarAsync();
        var taxa = await a.Taxas().CriarAsync(new("Taxa", 5m, ModalidadeTaxa.Fixa), Token);
        var casos = a.Taxas(alugueis: new AluguelAberto());
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.AlterarAsync(taxa.Id, new("Outro", 9m, ModalidadeTaxa.Diaria), Versao(taxa), Token));
        Assert.AreEqual("taxa_com_aluguel_aberto", erro.Codigo);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => casos.ExcluirAsync(taxa.Id, Versao(taxa), Token));
        var atual = await a.Taxas().ConsultarAsync(taxa.Id, Token);
        Assert.AreEqual(5m, atual.Preco);
        Assert.IsNull(atual.ExcluidoEmUtc);
        CollectionAssert.AreEqual(taxa.RowVersion, atual.RowVersion);
        Assert.AreEqual(1, await a.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task FalhaAposSaveChangesDesfazTaxaSeguroCombustivelEAuditoria()
    {
        await using var a = await Ambiente.CriarAsync();
        var taxa = await a.Taxas().CriarAsync(new("Taxa", 5m, ModalidadeTaxa.Fixa), Token);
        var seguro = await a.Seguros().SalvarAsync(TipoSeguro.Cliente, 7m, null, Token);
        await a.Combustiveis().RegistrarAsync(TipoCombustivel.Gasolina, 6m, Token);
        var falha = new AuditoriaQueFalha(a.Banco, a.Auditoria);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            a.Taxas(falha).CriarAsync(new("Outra", 1m, ModalidadeTaxa.Diaria), Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            a.Taxas(falha).AlterarAsync(taxa.Id, new("Outro", 8m, ModalidadeTaxa.Diaria), Versao(taxa), Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => a.Taxas(falha).ExcluirAsync(taxa.Id, Versao(taxa), Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            a.Seguros(falha).SalvarAsync(TipoSeguro.Cliente, 8m, Versao(seguro), Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            a.Seguros(falha).SalvarAsync(TipoSeguro.Terceiros, 1m, null, Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            a.Combustiveis(falha).RegistrarAsync(TipoCombustivel.Gasolina, 8m, Token));
        Assert.AreEqual(6, falha.Gravacoes);

        await using var leitura = AmbienteIntegracao.CriarContexto(contexto: a.Contexto);
        var taxaPersistida = await leitura.TaxasServicos.SingleAsync();
        Assert.AreEqual(5m, taxaPersistida.Preco);
        Assert.IsNull(taxaPersistida.ExcluidoEmUtc);
        CollectionAssert.AreEqual(taxa.RowVersion, taxaPersistida.RowVersion);
        var seguroPersistido = await leitura.PrecosSeguros.SingleAsync();
        Assert.AreEqual(7m, seguroPersistido.PrecoDiario);
        CollectionAssert.AreEqual(seguro.RowVersion, seguroPersistido.RowVersion);
        Assert.AreEqual(6m, (await leitura.PrecosCombustiveis.SingleAsync()).PrecoUnitario);
        Assert.AreEqual(3, await leitura.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task LockDaEmpresaImpedeMutacaoDePrecosAteLiberacao()
    {
        await using var a = await Ambiente.CriarAsync();
        await using var bloqueio = AmbienteIntegracao.CriarContexto(contexto: a.Contexto);
        await using var transacao = await bloqueio.Database.BeginTransactionAsync();
        await bloqueio.Database.SqlQuery<Guid>(
            $"SELECT [Id] AS [Value] FROM [Empresas] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {a.Contexto.EmpresaId!.Value}")
            .SingleAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            a.Combustiveis().RegistrarAsync(TipoCombustivel.Diesel, 5m, limite.Token));
        Assert.AreEqual(0, await a.Banco.PrecosCombustiveis.CountAsync());
        Assert.AreEqual(0, await a.Banco.AuditoriasCadastro.CountAsync());
        await transacao.RollbackAsync();
        await a.Combustiveis().RegistrarAsync(TipoCombustivel.Diesel, 5m, Token);
        Assert.AreEqual(1, await a.Banco.PrecosCombustiveis.CountAsync());
    }

    private sealed class AluguelAberto : IVinculosAluguelTaxa
    {
        public Task<bool> PossuiAluguelAbertoAsync(Guid id, CancellationToken cancelamento) => Task.FromResult(true);
    }

    private sealed class AuditoriaQueFalha(LocadoraDbContext banco, IAuditoriaCadastro original) : IAuditoriaCadastro
    {
        public int Gravacoes { get; private set; }
        public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid id, params string[] campos)
        {
            original.Registrar(acao, entidade, id, campos);
            Assert.IsNotNull(banco.Database.CurrentTransaction);
            banco.SaveChanges();
            Gravacoes++;
            throw new InvalidOperationException("Falha controlada antes do commit.");
        }
    }

    private sealed class Ambiente : IAsyncDisposable
    {
        public ContextoEmpresa Contexto { get; }
        public LocadoraDbContext Banco { get; }
        public RegistroAuditoriaCadastro Auditoria { get; }
        private readonly UnidadeTrabalhoEmpresa _unidade;
        private Ambiente(ContextoEmpresa contexto, LocadoraDbContext banco)
        {
            Contexto = contexto;
            Banco = banco;
            Auditoria = new(banco, contexto, TimeProvider.System);
            _unidade = new(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
        }
        public CasosTaxasServicos Taxas(IAuditoriaCadastro? auditoria = null, IVinculosAluguelTaxa? alugueis = null) =>
            new(new RepositorioTaxasServicos(Banco), _unidade, auditoria ?? Auditoria, Contexto,
                alugueis ?? new VinculosAluguelTaxaFase2(), TimeProvider.System);
        public CasosPrecosSeguros Seguros(IAuditoriaCadastro? auditoria = null) =>
            new(new RepositorioPrecosSeguros(Banco), _unidade, auditoria ?? Auditoria, Contexto, TimeProvider.System);
        public CasosPrecosCombustiveis Combustiveis(IAuditoriaCadastro? auditoria = null) =>
            new(new RepositorioPrecosCombustiveis(Banco), _unidade, auditoria ?? Auditoria, Contexto, TimeProvider.System);
        public static async Task<Ambiente> CriarAsync()
        {
            var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true, CorrelacaoId = Guid.NewGuid() };
            var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto);
            var empresa = Empresa.Nova();
            var nome = "precos_" + Guid.NewGuid().ToString("N");
            var conta = new UsuarioConta { Id = Guid.NewGuid(), EmpresaId = empresa.Id, UserName = nome, NormalizedUserName = nome.ToUpperInvariant() };
            contexto.EmpresaId = empresa.Id;
            contexto.ContaId = conta.Id;
            banco.AddRange(empresa, conta);
            await banco.SaveChangesAsync();
            contexto.PermiteCriarEmpresa = false;
            return new Ambiente(contexto, banco);
        }
        public ValueTask DisposeAsync() => Banco.DisposeAsync();
    }
}
