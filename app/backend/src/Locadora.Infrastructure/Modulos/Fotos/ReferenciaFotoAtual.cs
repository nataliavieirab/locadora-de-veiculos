using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Fotos;

public sealed class ReferenciaFotoAtual(LocadoraDbContext banco) : IReferenciaFotoAtual
{
    public async Task<(Veiculo? Veiculo, FotoObjeto? Foto)> ConsultarAsync(Guid id, CancellationToken token)
    {
        var veiculo = await banco.Veiculos.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id, token);
        var foto = veiculo?.FotoAtivaId is Guid fotoId
            ? await banco.FotosObjetos.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fotoId, token) : null;
        return (veiculo, foto);
    }
}
