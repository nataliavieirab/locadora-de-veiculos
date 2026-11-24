using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public sealed class CasosConsultaAluguel(IConsultasAluguel consultas)
{
    public Task<Pagina<ItemListaAluguel>> ListarAsync(Paginacao paginacao, FiltroConsultaAlugueis filtro,
        CancellationToken cancelamento)
    {
        ValidarPeriodo(filtro);
        ValidarEstado(filtro);
        return consultas.ListarAsync(paginacao, filtro, cancelamento);
    }

    public Task<ResultadoContratacao> ConsultarAsync(Guid id, CancellationToken cancelamento)
        => consultas.ConsultarAsync(id, cancelamento);

    private static void ValidarEstado(FiltroConsultaAlugueis filtro)
    {
        if (filtro.Estado is not null and not EstadoAluguel.EmAberto)
        {
            throw new ValidacaoCadastroException("estado", "Informe um estado válido para o recorte atual.");
        }
    }

    private static void ValidarPeriodo(FiltroConsultaAlugueis filtro)
    {
        if (filtro.SaidaDe is DateTimeOffset de && filtro.SaidaAte is DateTimeOffset ate && de >= ate)
        {
            throw new ValidacaoCadastroException("periodo", "O início do período deve ser anterior ao limite exclusivo informado.");
        }
    }
}