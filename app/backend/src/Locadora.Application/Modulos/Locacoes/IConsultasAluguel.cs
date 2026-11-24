using Locadora.Application.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public interface IConsultasAluguel : IConsultasHistoricoAluguel
{
    Task<Pagina<ItemListaAluguel>> ListarAsync(Paginacao paginacao, FiltroConsultaAlugueis filtro, CancellationToken cancelamento);
    Task<ResultadoConsultaAluguel> ConsultarAsync(Guid id, CancellationToken cancelamento);
}
