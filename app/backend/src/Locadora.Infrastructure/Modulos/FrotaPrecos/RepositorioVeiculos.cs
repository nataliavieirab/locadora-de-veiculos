using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.FrotaPrecos;

public sealed class RepositorioVeiculos(LocadoraDbContext banco) : IRepositorioVeiculos
{
    public async Task<Pagina<Veiculo>> ListarAsync(Paginacao paginacao, Guid? grupoId, CancellationToken cancelamento)
    {
        var consulta = banco.Veiculos.AsNoTracking().Where(item => grupoId == null || item.GrupoId == grupoId);
        return new Pagina<Veiculo>(await consulta.OrderBy(item => item.PlacaNormalizada).ThenBy(item => item.Id)
            .Skip(paginacao.Deslocamento).Take(paginacao.PageSize).ToListAsync(cancelamento),
            paginacao.Page, paginacao.PageSize, await consulta.CountAsync(cancelamento));
    }

    public Task<Veiculo?> ConsultarAsync(Guid id, CancellationToken cancelamento) =>
        banco.Veiculos.SingleOrDefaultAsync(item => item.Id == id, cancelamento);

    public Task<bool> PlacaEmUsoAsync(string placaNormalizada, Guid? excetoId, CancellationToken cancelamento) =>
        banco.Veiculos.AnyAsync(item => item.PlacaNormalizada == placaNormalizada && item.Id != excetoId, cancelamento);

    public void Adicionar(Veiculo veiculo) => banco.Veiculos.Add(veiculo);
}

// A F3 substituirá este adaptador pela consulta ao agregado Aluguel.
public sealed class VinculosAluguelVeiculoFase2 : IVinculosAluguelVeiculo
{
    public Task<bool> PossuiAluguelAbertoAsync(Guid veiculoId, CancellationToken cancelamento)
    {
        cancelamento.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
