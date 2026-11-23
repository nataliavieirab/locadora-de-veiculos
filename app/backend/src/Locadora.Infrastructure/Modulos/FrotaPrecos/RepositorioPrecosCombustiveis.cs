using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.FrotaPrecos;

public sealed class RepositorioPrecosCombustiveis(LocadoraDbContext banco) : IRepositorioPrecosCombustiveis
{
    public async Task<IReadOnlyList<PrecoCombustivel>> ListarAsync(TipoCombustivel? tipo, CancellationToken cancelamento) =>
        await banco.PrecosCombustiveis.AsNoTracking()
            .Where(item => tipo == null || item.Tipo == tipo)
            .OrderBy(item => item.Tipo)
            .ThenBy(item => item.InicioVigenciaUtc)
            .ThenBy(item => item.Ordem)
            .ThenBy(item => item.Id)
            .ToListAsync(cancelamento);

    public Task<PrecoCombustivel?> VigenteAsync(TipoCombustivel tipo, CancellationToken cancelamento) =>
        banco.PrecosCombustiveis.AsNoTracking()
            .Where(item => item.Tipo == tipo)
            .OrderByDescending(item => item.InicioVigenciaUtc)
            .ThenByDescending(item => item.Ordem)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancelamento);

    public Task<PrecoCombustivel?> AplicavelAsync(TipoCombustivel tipo, DateTimeOffset momento,
        CancellationToken cancelamento) =>
        banco.PrecosCombustiveis.AsNoTracking()
            .Where(item => item.Tipo == tipo && item.InicioVigenciaUtc <= momento)
            .OrderByDescending(item => item.InicioVigenciaUtc)
            .ThenByDescending(item => item.Ordem)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancelamento);

    public void Adicionar(PrecoCombustivel preco) => banco.PrecosCombustiveis.Add(preco);
}
