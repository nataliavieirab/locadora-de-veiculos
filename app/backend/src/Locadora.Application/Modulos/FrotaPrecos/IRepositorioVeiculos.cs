using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;

namespace Locadora.Application.Modulos.FrotaPrecos;

public interface IRepositorioVeiculos
{
    Task<Pagina<Veiculo>> ListarAsync(Paginacao paginacao, Guid? grupoId, CancellationToken cancelamento);
    Task<Veiculo?> ConsultarAsync(Guid id, CancellationToken cancelamento);
    Task<bool> PlacaEmUsoAsync(string placaNormalizada, Guid? excetoId, CancellationToken cancelamento);
    void Adicionar(Veiculo veiculo);
}

public interface IVinculosAluguelVeiculo
{
    Task<bool> PossuiAluguelAbertoAsync(Guid veiculoId, CancellationToken cancelamento);
}
