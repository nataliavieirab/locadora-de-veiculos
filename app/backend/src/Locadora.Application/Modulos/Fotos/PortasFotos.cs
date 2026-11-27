using Locadora.Domain.Modulos.Fotos;

namespace Locadora.Application.Modulos.Fotos;

public interface IRepositorioFotos
{
    Task<FotoObjeto?> ConsultarAsync(Guid id, CancellationToken cancelamento);
    Task<TrabalhoObjeto?> TrabalhoAsync(Guid fotoId, CancellationToken cancelamento);
    void Adicionar(FotoObjeto foto, TrabalhoObjeto trabalho);
    // Flush SQL dentro da mesma transação para liberar o índice único de ativa
    // antes da ativação seguinte; não faz commit nem I/O de objeto.
    Task SalvarAsync(CancellationToken cancelamento);
}

public interface ISessaoFotos
{
    Guid AtorId { get; }
    string Correlacao { get; }
    Task ExigirAsync(CancellationToken cancelamento);
}

public sealed class SessaoFotoInvalidaException() : Exception("Sessão inválida.");

public sealed record DestinoFotos(Locadora.Domain.Modulos.Fotos.ProvedorFoto Provedor, string Bucket);

public interface IRetomadaFotos
{
    Task RetomarAsync(CancellationToken cancelamento);
}
