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

    public Task<ResultadoConsultaAluguel> ConsultarAsync(Guid id, CancellationToken cancelamento)
        => consultas.ConsultarAsync(id, cancelamento);

    public Task<ResultadoConsultaAluguel> ConsultarAsync(Guid id, bool incluirOcultos, CancellationToken cancelamento)
        => consultas.ConsultarAsync(id, incluirOcultos, cancelamento);

    public Task<Pagina<ReferenciaVeiculoHistorico>> ReferenciasVeiculosAsync(Paginacao paginacao,
        bool incluirOcultos, CancellationToken cancelamento)
        => consultas.ReferenciasVeiculosAsync(paginacao, incluirOcultos, cancelamento);

    private static void ValidarEstado(FiltroConsultaAlugueis filtro)
    {
        if (filtro.Estado is { } estado) ValidacaoCadastro.Modalidade(estado, "estado");
    }

    private static void ValidarPeriodo(FiltroConsultaAlugueis filtro)
    {
        if (filtro.SaidaDe is DateTimeOffset de && filtro.SaidaAte is DateTimeOffset ate && de >= ate)
        {
            throw new ValidacaoCadastroException("periodo", "O início do período deve ser anterior ao limite exclusivo informado.");
        }
    }
}
