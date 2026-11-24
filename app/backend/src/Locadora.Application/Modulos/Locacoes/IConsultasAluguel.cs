using Locadora.Application.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public interface IConsultasAluguel
{
    Task<Pagina<ItemListaAluguel>> ListarAsync(Paginacao paginacao, FiltroConsultaAlugueis filtro, CancellationToken cancelamento);
    Task<ResultadoContratacao> ConsultarAsync(Guid id, CancellationToken cancelamento);
}