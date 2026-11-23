using Locadora.Domain.Modulos.FrotaPrecos;

namespace Locadora.Application.Modulos.FrotaPrecos;

public interface IRepositorioPrecosSeguros
{
    Task<IReadOnlyList<PrecoSeguro>> ListarSegurosAsync(CancellationToken cancelamento);
    Task<PrecoSeguro?> SeguroAsync(TipoSeguro tipo, CancellationToken cancelamento);
    void Adicionar(PrecoSeguro seguro);
}
