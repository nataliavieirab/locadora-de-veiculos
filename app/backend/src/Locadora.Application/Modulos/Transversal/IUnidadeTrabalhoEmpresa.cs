namespace Locadora.Application.Modulos.Transversal;

public interface IUnidadeTrabalhoEmpresa
{
    // O comando relê os dados dentro do lock e pode ser repetido integralmente.
    // Não deve produzir efeitos externos nem capturar entidades de outra tentativa.
    Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento);
}
