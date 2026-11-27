using Locadora.Domain.Modulos.FrotaPrecos;

namespace Locadora.Application.Modulos.Fotos;

public interface IInativacaoFotoVeiculo
{
    // Executada na unidade de trabalho de exclusão, sem acesso ao armazenamento.
    Task RetirarAsync(Veiculo veiculo, DateTimeOffset agora, CancellationToken cancelamento);
}
