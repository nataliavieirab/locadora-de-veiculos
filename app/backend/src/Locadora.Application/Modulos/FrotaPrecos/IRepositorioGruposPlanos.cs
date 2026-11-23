using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;

namespace Locadora.Application.Modulos.FrotaPrecos;

public interface IRepositorioGruposPlanos
{
    Task<Pagina<Grupo>> ListarGruposAsync(Paginacao paginacao, CancellationToken cancelamento);
    Task<Pagina<Plano>> ListarPlanosAsync(Paginacao paginacao, Guid? grupoId, CancellationToken cancelamento);
    Task<IReadOnlyList<Plano>> PlanosDoGrupoAsync(Guid grupoId, CancellationToken cancelamento);
    Task<Grupo?> GrupoAsync(Guid id, CancellationToken cancelamento);
    Task<Plano?> PlanoAsync(Guid id, CancellationToken cancelamento);
    Task<bool> GrupoPossuiVinculosAsync(Guid id, CancellationToken cancelamento);
    Task<bool> ModalidadeEmUsoAsync(Guid grupoId, ModalidadePlano modalidade, CancellationToken cancelamento);
    void Adicionar(Grupo grupo);
    void Adicionar(Plano plano);
}

public interface IVinculosAluguelPlano
{
    Task<bool> PlanoFoiContratadoAsync(Guid planoId, CancellationToken cancelamento);
}
