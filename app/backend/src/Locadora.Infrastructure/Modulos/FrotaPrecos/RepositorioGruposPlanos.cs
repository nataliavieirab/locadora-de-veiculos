using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.FrotaPrecos;

public sealed class RepositorioGruposPlanos(LocadoraDbContext banco) : IRepositorioGruposPlanos
{
    public async Task<Pagina<Grupo>> ListarGruposAsync(Paginacao paginacao, CancellationToken cancelamento)
    {
        var consulta = banco.Grupos.AsNoTracking();
        return new Pagina<Grupo>(await consulta.OrderBy(item => item.Nome).ThenBy(item => item.Id)
            .Skip(paginacao.Deslocamento).Take(paginacao.PageSize).ToListAsync(cancelamento),
            paginacao.Page, paginacao.PageSize, await consulta.CountAsync(cancelamento));
    }

    public async Task<Pagina<Plano>> ListarPlanosAsync(Paginacao paginacao, Guid? grupoId, CancellationToken cancelamento)
    {
        var consulta = banco.Planos.AsNoTracking().Where(item => grupoId == null || item.GrupoId == grupoId);
        return new Pagina<Plano>(await consulta.OrderBy(item => item.GrupoId).ThenBy(item => item.Modalidade).ThenBy(item => item.Id)
            .Skip(paginacao.Deslocamento).Take(paginacao.PageSize).ToListAsync(cancelamento),
            paginacao.Page, paginacao.PageSize, await consulta.CountAsync(cancelamento));
    }

    public async Task<IReadOnlyList<Plano>> PlanosDoGrupoAsync(Guid grupoId, CancellationToken cancelamento) =>
        await banco.Planos.AsNoTracking().Where(item => item.GrupoId == grupoId).OrderBy(item => item.Modalidade).ThenBy(item => item.Id).ToListAsync(cancelamento);

    public Task<Grupo?> GrupoAsync(Guid id, CancellationToken cancelamento) =>
        banco.Grupos.SingleOrDefaultAsync(item => item.Id == id, cancelamento);
    public Task<Plano?> PlanoAsync(Guid id, CancellationToken cancelamento) =>
        banco.Planos.SingleOrDefaultAsync(item => item.Id == id, cancelamento);
    public async Task<bool> GrupoPossuiVinculosAsync(Guid id, CancellationToken cancelamento) =>
        await banco.Planos.AnyAsync(item => item.GrupoId == id, cancelamento)
        || await banco.Veiculos.AnyAsync(item => item.GrupoId == id, cancelamento);
    public Task<bool> ModalidadeEmUsoAsync(Guid grupoId, ModalidadePlano modalidade, CancellationToken cancelamento) =>
        banco.Planos.AnyAsync(item => item.GrupoId == grupoId && item.Modalidade == modalidade, cancelamento);
    public void Adicionar(Grupo grupo) => banco.Grupos.Add(grupo);
    public void Adicionar(Plano plano) => banco.Planos.Add(plano);
}

// A F3 adicionará a consulta real a contratos abertos e concluídos.
public sealed class VinculosAluguelPlanoFase2 : IVinculosAluguelPlano
{
    public Task<bool> PlanoFoiContratadoAsync(Guid planoId, CancellationToken cancelamento)
    {
        cancelamento.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
