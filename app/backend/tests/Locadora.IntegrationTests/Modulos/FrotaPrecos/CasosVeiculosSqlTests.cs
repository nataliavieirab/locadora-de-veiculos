using System.Text.Json;
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
public sealed class CasosVeiculosSqlTests
{
    private static readonly CancellationToken Token = CancellationToken.None;
    private static DadosVeiculo Dados(Guid grupoId) =>
        new("abc-1D23", "Marca sintética", "Azul", "Modelo sintético", TipoCombustivel.Gasolina, 48.125m, 2026, grupoId);
    private static string Versao(EntidadeCadastro entidade) => VersaoCadastro.Etag(entidade.RowVersion);

    [TestMethod]
    public async Task CriaEditaPaginaFiltraEAuditaSomenteCamposAlterados()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var primeiro = await ambiente.Casos.CriarAsync(Dados(ambiente.Grupo.Id), Token);
        var segundo = await ambiente.Casos.CriarAsync(Dados(ambiente.Grupo.Id) with { Placa = "XYZ-9A87" }, Token);
        Assert.AreEqual(8, primeiro.RowVersion.Length);
        var pagina = await ambiente.Casos.ListarAsync(new Paginacao(2, 1), ambiente.Grupo.Id, Token);
        Assert.AreEqual(2, pagina.Total);
        Assert.AreEqual(segundo.Id, pagina.Items.Single().Id);
        Assert.AreEqual(0, (await ambiente.Casos.ListarAsync(new Paginacao(), Guid.NewGuid(), Token)).Total);

        var dados = Dados(ambiente.Grupo.Id) with { Cor = "Prata" };
        var alterado = await ambiente.Casos.AlterarAsync(primeiro.Id, dados, Versao(primeiro), Token);
        Assert.AreEqual("Prata", alterado.Cor);
        Assert.IsFalse(primeiro.RowVersion.SequenceEqual(alterado.RowVersion));
        var evento = await ambiente.Banco.AuditoriasCadastro.SingleAsync(item =>
            item.EntidadeId == primeiro.Id && item.Acao == AcaoCadastro.Alteracao);
        Assert.AreEqual("cor", evento.CamposAlterados);
        Assert.AreEqual(ambiente.Contexto.ContaId, evento.ContaId);
        Assert.AreEqual(ambiente.Contexto.CorrelacaoId, evento.CorrelacaoId);
        Assert.DoesNotContain(JsonSerializer.Serialize(evento), "Prata");
        var antes = await ambiente.Banco.AuditoriasCadastro.CountAsync();
        var semMudanca = await ambiente.Casos.AlterarAsync(primeiro.Id, dados, Versao(alterado), Token);
        CollectionAssert.AreEqual(alterado.RowVersion, semMudanca.RowVersion);
        Assert.AreEqual(antes, await ambiente.Banco.AuditoriasCadastro.CountAsync());
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            ambiente.Casos.ExcluirAsync(primeiro.Id, Versao(primeiro), Token));
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            ambiente.Casos.AlterarAsync(primeiro.Id, dados, null, Token));
    }

    [TestMethod]
    public async Task IsolaLeituraEscritaGrupoEPermiteMesmaPlacaEmEmpresasDiferentes()
    {
        await using var a = await Ambiente.CriarAsync();
        await using var b = await Ambiente.CriarAsync();
        var veiculoA = await a.Casos.CriarAsync(Dados(a.Grupo.Id), Token);
        var veiculoB = await b.Casos.CriarAsync(Dados(b.Grupo.Id), Token);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Casos.ConsultarAsync(veiculoB.Id, Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            a.Casos.AlterarAsync(veiculoB.Id, Dados(a.Grupo.Id), Versao(veiculoB), Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            a.Casos.ExcluirAsync(veiculoB.Id, Versao(veiculoB), Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            a.Casos.CriarAsync(Dados(b.Grupo.Id) with { Placa = "XYZ-9A87" }, Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            a.Casos.AlterarAsync(veiculoA.Id, Dados(b.Grupo.Id), Versao(veiculoA), Token));
        Assert.AreEqual(0, (await a.Casos.ListarAsync(new Paginacao(), b.Grupo.Id, Token)).Total);
        Assert.AreEqual(1, (await a.Casos.ListarAsync(new Paginacao(), null, Token)).Total);
        Assert.AreEqual(1, await a.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(b.Grupo.Id, (await b.Casos.ConsultarAsync(veiculoB.Id, Token)).GrupoId);
    }

    [TestMethod]
    public async Task DuplicidadeNormalizadaNaoAlteraAtivosEExclusaoLiberaNovaIdentidade()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var original = await ambiente.Casos.CriarAsync(Dados(ambiente.Grupo.Id), Token);
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.CriarAsync(Dados(ambiente.Grupo.Id) with { Placa = "ABC1D23" }, Token));
        Assert.AreEqual("placa", erro.Campo);
        var segundo = await ambiente.Casos.CriarAsync(Dados(ambiente.Grupo.Id) with { Placa = "XYZ-9A87" }, Token);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.AlterarAsync(segundo.Id, Dados(ambiente.Grupo.Id), Versao(segundo), Token));
        await ambiente.Casos.ExcluirAsync(original.Id, Versao(original), Token);
        var novo = await ambiente.Casos.CriarAsync(Dados(ambiente.Grupo.Id), Token);
        Assert.AreNotEqual(original.Id, novo.Id);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => ambiente.Casos.ConsultarAsync(original.Id, Token));
        var historico = await ambiente.Banco.Veiculos.IgnoreQueryFilters()
            .SingleAsync(item => item.EmpresaId == ambiente.Contexto.EmpresaId && item.Id == original.Id);
        Assert.IsNotNull(historico.ExcluidoEmUtc);
        Assert.AreEqual("abc-1D23", historico.Placa);
        Assert.AreEqual(2, await ambiente.Banco.AuditoriasCadastro.CountAsync(item => item.EntidadeId == original.Id));
        Assert.AreEqual(1, await ambiente.Banco.AuditoriasCadastro.CountAsync(item => item.EntidadeId == novo.Id));
    }

    [TestMethod]
    public async Task PortaControladaBloqueiaEdicaoEExclusaoPorAluguelSemEfeitoParcial()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var veiculo = await ambiente.Casos.CriarAsync(Dados(ambiente.Grupo.Id), Token);
        var casos = ambiente.CriarCasos(alugueis: new AluguelAberto());
        var edicao = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.AlterarAsync(veiculo.Id, Dados(ambiente.Grupo.Id) with { Cor = "Prata" }, Versao(veiculo), Token));
        Assert.AreEqual("veiculo_com_aluguel_aberto", edicao.Codigo);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => casos.ExcluirAsync(veiculo.Id, Versao(veiculo), Token));
        var atual = await ambiente.Casos.ConsultarAsync(veiculo.Id, Token);
        Assert.AreEqual("Azul", atual.Cor);
        Assert.IsNull(atual.ExcluidoEmUtc);
        CollectionAssert.AreEqual(veiculo.RowVersion, atual.RowVersion);
        Assert.AreEqual(1, await ambiente.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task FalhaAposSaveChangesDesfazVeiculoEAuditoriaNaCriacaoEdicaoEExclusao()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var veiculo = await ambiente.Casos.CriarAsync(Dados(ambiente.Grupo.Id), Token);
        var falha = new AuditoriaQueFalha(ambiente.Banco, ambiente.Auditoria);
        var casos = ambiente.CriarCasos(auditoria: falha);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            casos.CriarAsync(Dados(ambiente.Grupo.Id) with { Placa = "XYZ-9A87" }, Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            casos.AlterarAsync(veiculo.Id, Dados(ambiente.Grupo.Id) with { Cor = "Prata" }, Versao(veiculo), Token));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            casos.ExcluirAsync(veiculo.Id, Versao(veiculo), Token));
        Assert.AreEqual(3, falha.GravacoesAntesDaFalha);
        await using var leitura = AmbienteIntegracao.CriarContexto(contexto: ambiente.Contexto);
        var atual = await leitura.Veiculos.SingleAsync();
        Assert.AreEqual(veiculo.Id, atual.Id);
        Assert.AreEqual("Azul", atual.Cor);
        CollectionAssert.AreEqual(veiculo.RowVersion, atual.RowVersion);
        Assert.AreEqual(1, await leitura.AuditoriasCadastro.CountAsync());
    }

    private sealed class AluguelAberto : IVinculosAluguelVeiculo
    {
        public Task<bool> PossuiAluguelAbertoAsync(Guid id, CancellationToken cancelamento) => Task.FromResult(true);
    }

    private sealed class AuditoriaQueFalha(LocadoraDbContext banco, IAuditoriaCadastro original) : IAuditoriaCadastro
    {
        public int GravacoesAntesDaFalha { get; private set; }
        public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid id, params string[] campos)
        {
            original.Registrar(acao, entidade, id, campos);
            Assert.IsNotNull(banco.Database.CurrentTransaction);
            banco.SaveChanges();
            GravacoesAntesDaFalha++;
            throw new InvalidOperationException("Falha controlada após gravação, antes do commit.");
        }
    }

    private sealed class Ambiente : IAsyncDisposable
    {
        public ContextoEmpresa Contexto { get; }
        public LocadoraDbContext Banco { get; }
        public RegistroAuditoriaCadastro Auditoria { get; }
        public Grupo Grupo { get; }
        public CasosVeiculos Casos { get; }

        private Ambiente(ContextoEmpresa contexto, LocadoraDbContext banco, Grupo grupo)
        {
            Contexto = contexto;
            Banco = banco;
            Grupo = grupo;
            Auditoria = new RegistroAuditoriaCadastro(banco, contexto, TimeProvider.System);
            Casos = CriarCasos();
        }

        public CasosVeiculos CriarCasos(IAuditoriaCadastro? auditoria = null, IVinculosAluguelVeiculo? alugueis = null) =>
            new(new RepositorioVeiculos(Banco), new RepositorioGruposPlanos(Banco),
                new UnidadeTrabalhoEmpresa(Banco, Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
                auditoria ?? Auditoria, Contexto, alugueis ?? new VinculosAluguelVeiculoFase2(), TimeProvider.System);

        public static async Task<Ambiente> CriarAsync()
        {
            var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true, CorrelacaoId = Guid.NewGuid() };
            var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto);
            var empresa = Empresa.Nova();
            var nome = "veiculos_" + Guid.NewGuid().ToString("N");
            var conta = new UsuarioConta { Id = Guid.NewGuid(), EmpresaId = empresa.Id, UserName = nome, NormalizedUserName = nome.ToUpperInvariant() };
            var grupo = Grupo.Novo(empresa.Id, "Grupo sintético", DateTimeOffset.UtcNow);
            contexto.EmpresaId = empresa.Id;
            contexto.ContaId = conta.Id;
            banco.AddRange(empresa, conta, grupo);
            await banco.SaveChangesAsync();
            contexto.PermiteCriarEmpresa = false;
            return new Ambiente(contexto, banco, grupo);
        }

        public ValueTask DisposeAsync() => Banco.DisposeAsync();
    }
}
