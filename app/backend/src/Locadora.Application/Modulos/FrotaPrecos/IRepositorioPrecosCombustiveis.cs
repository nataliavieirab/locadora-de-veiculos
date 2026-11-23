using Locadora.Domain.Modulos.FrotaPrecos;

namespace Locadora.Application.Modulos.FrotaPrecos;

public interface IRepositorioPrecosCombustiveis
{
    Task<IReadOnlyList<PrecoCombustivel>> ListarAsync(TipoCombustivel? tipo, CancellationToken cancelamento);
    Task<PrecoCombustivel?> VigenteAsync(TipoCombustivel tipo, CancellationToken cancelamento);
    Task<PrecoCombustivel?> AplicavelAsync(TipoCombustivel tipo, DateTimeOffset momento, CancellationToken cancelamento);
    void Adicionar(PrecoCombustivel preco);
}
