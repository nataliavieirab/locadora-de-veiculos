using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.FrotaPrecos;

public sealed record PrecosPlano(decimal PrecoDiario, decimal? PrecoKm, int? FranquiaDiariaKm, decimal? PrecoKmExcedente);

public sealed class CasosGruposPlanos(IRepositorioGruposPlanos repositorio, IUnidadeTrabalhoEmpresa unidade,
    IAuditoriaCadastro auditoria, IContextoCadastro contexto, IVinculosAluguelPlano alugueis, TimeProvider tempo)
{
    public Task<Pagina<Grupo>> ListarGruposAsync(Paginacao paginacao, CancellationToken cancelamento) =>
        repositorio.ListarGruposAsync(paginacao, cancelamento);

    public Task<Pagina<Plano>> ListarPlanosAsync(Paginacao paginacao, Guid? grupoId, CancellationToken cancelamento) =>
        repositorio.ListarPlanosAsync(paginacao, grupoId, cancelamento);

    public async Task<Grupo> GrupoAsync(Guid id, CancellationToken cancelamento) =>
        await repositorio.GrupoAsync(id, cancelamento) ?? throw new CadastroNaoEncontradoException();

    public async Task<Plano> PlanoAsync(Guid id, CancellationToken cancelamento) =>
        await repositorio.PlanoAsync(id, cancelamento) ?? throw new CadastroNaoEncontradoException();

    public async Task<IReadOnlyList<Plano>> PlanosDoGrupoAsync(Guid grupoId, CancellationToken cancelamento)
    {
        await GrupoAsync(grupoId, cancelamento);
        return await repositorio.PlanosDoGrupoAsync(grupoId, cancelamento);
    }

    public Task<Grupo> CriarGrupoAsync(string nome, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(token =>
        {
            var grupo = Grupo.Novo(contexto.EmpresaId, nome, tempo.GetUtcNow());
            repositorio.Adicionar(grupo);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.Grupo, grupo.Id, "nome");
            return Task.FromResult(grupo);
        }, cancelamento);

    public Task<Grupo> AlterarGrupoAsync(Guid id, string nome, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var grupo = await GrupoAsync(id, token);
            VersaoCadastro.Exigir(versao, grupo.RowVersion);
            var candidato = Grupo.Novo(contexto.EmpresaId, nome, tempo.GetUtcNow());
            if (grupo.Nome != candidato.Nome)
            {
                grupo.Alterar(nome, tempo.GetUtcNow());
                auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Grupo, id, "nome");
            }
            return grupo;
        }, cancelamento);

    public Task<bool> ExcluirGrupoAsync(Guid id, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var grupo = await GrupoAsync(id, token);
            VersaoCadastro.Exigir(versao, grupo.RowVersion);
            if (await repositorio.GrupoPossuiVinculosAsync(id, token))
            {
                throw new ConflitoCadastroException("grupo_com_vinculos");
            }
            grupo.Excluir(tempo.GetUtcNow());
            auditoria.Registrar(AcaoCadastro.Exclusao, TipoCadastro.Grupo, id, "excluidoEmUtc");
            return true;
        }, cancelamento);

    public Task<Plano> CriarPlanoAsync(Guid grupoId, ModalidadePlano modalidade, PrecosPlano precos, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            await GrupoAsync(grupoId, token);
            var plano = Plano.Novo(contexto.EmpresaId, grupoId, modalidade, precos.PrecoDiario,
                precos.PrecoKm, precos.FranquiaDiariaKm, precos.PrecoKmExcedente, tempo.GetUtcNow());
            if (await repositorio.ModalidadeEmUsoAsync(grupoId, modalidade, token))
            {
                throw new ConflitoCadastroException("cadastro_duplicado", "modalidade");
            }
            repositorio.Adicionar(plano);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.Plano, plano.Id,
                ["grupoId", "modalidade", .. CamposPrecos(plano)]);
            return plano;
        }, cancelamento);

    public Task<Plano> AlterarPlanoAsync(Guid id, PrecosPlano precos, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var plano = await PlanoAsync(id, token);
            VersaoCadastro.Exigir(versao, plano.RowVersion);
            await GrupoAsync(plano.GrupoId, token);
            var candidato = Plano.Novo(contexto.EmpresaId, plano.GrupoId, plano.Modalidade, precos.PrecoDiario,
                precos.PrecoKm, precos.FranquiaDiariaKm, precos.PrecoKmExcedente, tempo.GetUtcNow());
            var campos = new List<string>();
            if (plano.PrecoDiario != candidato.PrecoDiario) campos.Add("precoDiario");
            if (plano.PrecoKm != candidato.PrecoKm) campos.Add("precoKm");
            if (plano.FranquiaDiariaKm != candidato.FranquiaDiariaKm) campos.Add("franquiaDiariaKm");
            if (plano.PrecoKmExcedente != candidato.PrecoKmExcedente) campos.Add("precoKmExcedente");
            if (campos.Count != 0)
            {
                plano.Alterar(precos.PrecoDiario, precos.PrecoKm, precos.FranquiaDiariaKm, precos.PrecoKmExcedente, tempo.GetUtcNow());
                auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Plano, id, campos.ToArray());
            }
            return plano;
        }, cancelamento);

    public Task<bool> ExcluirPlanoAsync(Guid id, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var plano = await PlanoAsync(id, token);
            VersaoCadastro.Exigir(versao, plano.RowVersion);
            if (await alugueis.PlanoFoiContratadoAsync(id, token))
            {
                throw new ConflitoCadastroException("plano_ja_contratado");
            }
            plano.Excluir(tempo.GetUtcNow());
            auditoria.Registrar(AcaoCadastro.Exclusao, TipoCadastro.Plano, id, "excluidoEmUtc");
            return true;
        }, cancelamento);

    private static string[] CamposPrecos(Plano plano) => plano.Modalidade switch
    {
        ModalidadePlano.Diario => ["precoDiario", "precoKm"],
        ModalidadePlano.Controlado => ["precoDiario", "franquiaDiariaKm", "precoKmExcedente"],
        _ => ["precoDiario"],
    };
}
