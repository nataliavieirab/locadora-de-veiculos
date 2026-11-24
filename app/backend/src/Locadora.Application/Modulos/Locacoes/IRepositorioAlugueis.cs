using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;

namespace Locadora.Application.Modulos.Locacoes;

public interface IContextoLocacao : IContextoCadastro
{
    Guid ContaId { get; }
}

public interface IRepositorioAlugueis
{
    Task<Aluguel?> ConsultarAsync(Guid id, CancellationToken cancelamento);
    Task<ComandoIdempotente?> ComandoAsync(Guid contaId, Guid chave, CancellationToken cancelamento);
    Task<RevisaoContrato> RevisaoAsync(Guid id, CancellationToken cancelamento);
    void Adicionar(Aluguel aluguel);
    void Adicionar(ComandoIdempotente comando);
    Task<byte[]> SalvarAsync(Aluguel aluguel, CancellationToken cancelamento);
}
