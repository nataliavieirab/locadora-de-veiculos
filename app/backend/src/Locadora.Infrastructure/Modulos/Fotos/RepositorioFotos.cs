using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Fotos;

public sealed class RepositorioFotos(LocadoraDbContext banco) : IRepositorioFotos
{
    public Task<FotoObjeto?> ConsultarAsync(Guid id, CancellationToken cancelamento) =>
        banco.FotosObjetos.SingleOrDefaultAsync(f => f.Id == id, cancelamento);
    public Task<TrabalhoObjeto?> TrabalhoAsync(Guid fotoId, CancellationToken cancelamento) =>
        banco.TrabalhosObjetos.SingleOrDefaultAsync(t => t.FotoObjetoId == fotoId, cancelamento);
    public void Adicionar(FotoObjeto foto, TrabalhoObjeto trabalho) => banco.AddRange(foto, trabalho);
    public Task SalvarAsync(CancellationToken cancelamento) => banco.SaveChangesAsync(cancelamento);
}
