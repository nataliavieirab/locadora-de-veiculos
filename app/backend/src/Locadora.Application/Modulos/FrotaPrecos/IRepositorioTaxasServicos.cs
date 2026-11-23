using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;

namespace Locadora.Application.Modulos.FrotaPrecos;

public interface IRepositorioTaxasServicos
{
    Task<Pagina<TaxaServico>> ListarAsync(Paginacao paginacao, CancellationToken cancelamento);
    Task<TaxaServico?> ConsultarAsync(Guid id, CancellationToken cancelamento);
    void Adicionar(TaxaServico taxa);
}

public interface IVinculosAluguelTaxa
{
    Task<bool> PossuiAluguelAbertoAsync(Guid taxaId, CancellationToken cancelamento);
}
