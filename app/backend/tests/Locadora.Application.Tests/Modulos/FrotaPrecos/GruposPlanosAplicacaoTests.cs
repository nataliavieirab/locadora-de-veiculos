using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.FrotaPrecos;

[TestClass]
public sealed class GruposPlanosAplicacaoTests
{
    [TestMethod]
    public async Task CriaGrupoETresModalidadesSemDuplicarConfiguracao()
    {
        var ambiente = new Ambiente();
        var grupo = await ambiente.Casos.CriarGrupoAsync("Grupo sintético", CancellationToken.None);
        foreach (var (modalidade, precos) in new[]
        {
            (ModalidadePlano.Diario, new PrecosPlano(100m, 1m, null, null)),
            (ModalidadePlano.Controlado, new PrecosPlano(150m, null, 100, 2m)),
            (ModalidadePlano.Livre, new PrecosPlano(200m, null, null, null)),
        })
        {
            await ambiente.Casos.CriarPlanoAsync(grupo.Id, modalidade, precos, CancellationToken.None);
            await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
                ambiente.Casos.CriarPlanoAsync(grupo.Id, modalidade, precos, CancellationToken.None));
        }
        Assert.AreEqual(3, (await ambiente.Casos.PlanosDoGrupoAsync(grupo.Id, CancellationToken.None)).Count);
        Assert.AreEqual(4, ambiente.Auditoria.Eventos.Count);
    }

    [TestMethod]
    public async Task GrupoComPlanoOuVeiculoNaoPodeSerExcluido()
    {
        var ambiente = new Ambiente();
        var grupo = await ambiente.Casos.CriarGrupoAsync("Grupo", CancellationToken.None);
        ambiente.Repositorio.PossuiVinculos = true;
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.ExcluirGrupoAsync(grupo.Id, VersaoCadastro.Etag(grupo.RowVersion), CancellationToken.None));
        Assert.AreEqual("grupo_com_vinculos", erro.Codigo);
        Assert.IsNull(grupo.ExcluidoEmUtc);
        Assert.AreEqual(1, ambiente.Auditoria.Eventos.Count);
    }

    [TestMethod]
    public async Task PontoDeExtensaoBloqueiaPlanoContratadoSemCriarAluguelFicticio()
    {
        var ambiente = new Ambiente();
        var grupo = await ambiente.Casos.CriarGrupoAsync("Grupo", CancellationToken.None);
        var plano = await ambiente.Casos.CriarPlanoAsync(grupo.Id, ModalidadePlano.Livre,
            new PrecosPlano(100m, null, null, null), CancellationToken.None);
        ambiente.Alugueis.Contratado = true;
        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.ExcluirPlanoAsync(plano.Id, VersaoCadastro.Etag(plano.RowVersion), CancellationToken.None));
        Assert.AreEqual("plano_ja_contratado", erro.Codigo);
        Assert.IsNull(plano.ExcluidoEmUtc);
    }

    [TestMethod]
    public async Task EditarPrecoPreservaIdentidadeEAuditaSomenteCampoModificado()
    {
        var ambiente = new Ambiente();
        var grupo = await ambiente.Casos.CriarGrupoAsync("Grupo", CancellationToken.None);
        var plano = await ambiente.Casos.CriarPlanoAsync(grupo.Id, ModalidadePlano.Diario,
            new PrecosPlano(100m, 1m, null, null), CancellationToken.None);
        await ambiente.Casos.AlterarPlanoAsync(plano.Id, new PrecosPlano(150m, 1m, null, null),
            VersaoCadastro.Etag(plano.RowVersion), CancellationToken.None);
        Assert.AreEqual(150m, plano.PrecoDiario);
        Assert.AreEqual(1m, plano.PrecoKm);
        Assert.AreEqual("precoDiario", string.Join(',', ambiente.Auditoria.Eventos.Last().Campos));
        Assert.AreEqual(AcaoCadastro.Alteracao, ambiente.Auditoria.Eventos.Last().Acao);
    }

    [TestMethod]
    public async Task ExcluirGrupoSemVinculosEPlanoNuncaContratadoPreservaIds()
    {
        var ambiente = new Ambiente();
        var grupo = await ambiente.Casos.CriarGrupoAsync("Grupo", CancellationToken.None);
        var plano = await ambiente.Casos.CriarPlanoAsync(grupo.Id, ModalidadePlano.Livre,
            new PrecosPlano(0m, null, null, null), CancellationToken.None);
        await ambiente.Casos.ExcluirPlanoAsync(plano.Id, VersaoCadastro.Etag(plano.RowVersion), CancellationToken.None);
        await ambiente.Casos.ExcluirGrupoAsync(grupo.Id, VersaoCadastro.Etag(grupo.RowVersion), CancellationToken.None);
        Assert.IsNotNull(grupo.ExcluidoEmUtc);
        Assert.IsNotNull(plano.ExcluidoEmUtc);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => ambiente.Casos.GrupoAsync(grupo.Id, CancellationToken.None));
    }

    private sealed class Ambiente : IContextoCadastro
    {
        public Guid EmpresaId { get; } = Guid.NewGuid();
        public Repositorio Repositorio { get; } = new();
        public Auditoria Auditoria { get; } = new();
        public Alugueis Alugueis { get; } = new();
        public CasosGruposPlanos Casos { get; }
        public Ambiente() => Casos = new(Repositorio, new Unidade(), Auditoria, this, Alugueis, TimeProvider.System);
    }

    private sealed class Unidade : IUnidadeTrabalhoEmpresa
    {
        public Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento) => comando(cancelamento);
    }

    private sealed class Auditoria : IAuditoriaCadastro
    {
        public List<(AcaoCadastro Acao, string[] Campos)> Eventos { get; } = [];
        public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid id, params string[] campos) => Eventos.Add((acao, campos));
    }

    private sealed class Alugueis : IVinculosAluguelPlano
    {
        public bool Contratado { get; set; }
        public Task<bool> PlanoFoiContratadoAsync(Guid id, CancellationToken c) => Task.FromResult(Contratado);
    }

    private sealed class Repositorio : IRepositorioGruposPlanos
    {
        private readonly List<Grupo> _grupos = [];
        private readonly List<Plano> _planos = [];
        public bool PossuiVinculos { get; set; }
        public Task<Pagina<Grupo>> ListarGruposAsync(Paginacao p, CancellationToken c) =>
            Task.FromResult(new Pagina<Grupo>(_grupos.Where(item => item.ExcluidoEmUtc is null).ToArray(), p.Page, p.PageSize, _grupos.Count));
        public Task<Pagina<Plano>> ListarPlanosAsync(Paginacao p, Guid? grupoId, CancellationToken c) =>
            Task.FromResult(new Pagina<Plano>(_planos.Where(item => item.ExcluidoEmUtc is null && (grupoId is null || item.GrupoId == grupoId)).ToArray(), p.Page, p.PageSize, _planos.Count));
        public Task<IReadOnlyList<Plano>> PlanosDoGrupoAsync(Guid id, CancellationToken c) =>
            Task.FromResult<IReadOnlyList<Plano>>(_planos.Where(item => item.GrupoId == id && item.ExcluidoEmUtc is null).ToArray());
        public Task<Grupo?> GrupoAsync(Guid id, CancellationToken c) =>
            Task.FromResult(_grupos.SingleOrDefault(item => item.Id == id && item.ExcluidoEmUtc is null));
        public Task<Plano?> PlanoAsync(Guid id, CancellationToken c) =>
            Task.FromResult(_planos.SingleOrDefault(item => item.Id == id && item.ExcluidoEmUtc is null));
        public Task<bool> GrupoPossuiVinculosAsync(Guid id, CancellationToken c) =>
            Task.FromResult(PossuiVinculos || _planos.Any(item => item.GrupoId == id && item.ExcluidoEmUtc is null));
        public Task<bool> ModalidadeEmUsoAsync(Guid id, ModalidadePlano m, CancellationToken c) =>
            Task.FromResult(_planos.Any(item => item.GrupoId == id && item.Modalidade == m && item.ExcluidoEmUtc is null));
        public void Adicionar(Grupo grupo) { Preparar(grupo); _grupos.Add(grupo); }
        public void Adicionar(Plano plano) { Preparar(plano); _planos.Add(plano); }
        private static void Preparar(EntidadeCadastro entidade) =>
            typeof(EntidadeCadastro).GetProperty(nameof(EntidadeCadastro.RowVersion))!.SetValue(entidade, new byte[8]);
    }
}
