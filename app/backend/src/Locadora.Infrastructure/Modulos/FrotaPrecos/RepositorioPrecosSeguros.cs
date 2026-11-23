using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.FrotaPrecos;

public sealed class RepositorioPrecosSeguros(LocadoraDbContext banco) : IRepositorioPrecosSeguros
{
    public async Task<IReadOnlyList<PrecoSeguro>> ListarSegurosAsync(CancellationToken cancelamento) =>
        await banco.PrecosSeguros.AsNoTracking().OrderBy(item => item.Tipo).ThenBy(item => item.Id).ToListAsync(cancelamento);

    public Task<PrecoSeguro?> SeguroAsync(TipoSeguro tipo, CancellationToken cancelamento) =>
        banco.PrecosSeguros.SingleOrDefaultAsync(item => item.Tipo == tipo, cancelamento);

    public void Adicionar(PrecoSeguro seguro) => banco.PrecosSeguros.Add(seguro);
}
