using Locadora.Application.Modulos.Pessoas;

namespace Locadora.Infrastructure.Modulos.Pessoas;

// A F2 não contém aluguéis. A F3 substituirá este adaptador por consultas reais,
// dentro da mesma transação e do mesmo lock dos comandos cadastrais.
public sealed class VinculosAluguelPessoasFase2 : IVinculosAluguelPessoas
{
    public Task<bool> ClientePossuiAluguelAbertoAsync(Guid clienteId, CancellationToken cancelamento)
    {
        cancelamento.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public Task<bool> CondutorPossuiAluguelAbertoAsync(Guid condutorId, CancellationToken cancelamento)
    {
        cancelamento.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
