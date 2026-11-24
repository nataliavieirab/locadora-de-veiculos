using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;

namespace Locadora.Application.Modulos.Locacoes;

public interface IConsultasContratacao
{
    Task<DadosContratacao> CarregarAsync(ComandoContratacao comando, CancellationToken cancelamento);
    Task<Aluguel?> AluguelAsync(Guid id, CancellationToken cancelamento);
    Task<bool> VeiculoOcupadoAsync(Guid veiculoId, Guid? excetoAluguelId, CancellationToken cancelamento);
    Task<Pagina<Veiculo>> VeiculosAsync(Paginacao paginacao, Guid? grupoId, bool somenteDisponiveis,
        Guid? aluguelEmEdicaoId, CancellationToken cancelamento);
    Task<Pagina<Condutor>> CondutoresAsync(Paginacao paginacao, Guid clienteId, CancellationToken cancelamento);
}
