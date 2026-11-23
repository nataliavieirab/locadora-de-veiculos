using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.FrotaPrecos;

public sealed class RepositorioTaxasServicos(LocadoraDbContext banco) : IRepositorioTaxasServicos
{
    public async Task<Pagina<TaxaServico>> ListarAsync(Paginacao paginacao, CancellationToken cancelamento)
    {
        var consulta = banco.TaxasServicos.AsNoTracking();
        return new Pagina<TaxaServico>(await consulta.OrderBy(item => item.Nome).ThenBy(item => item.Id)
            .Skip(paginacao.Deslocamento).Take(paginacao.PageSize).ToListAsync(cancelamento),
            paginacao.Page, paginacao.PageSize, await consulta.CountAsync(cancelamento));
    }

    public Task<TaxaServico?> ConsultarAsync(Guid id, CancellationToken cancelamento) =>
        banco.TaxasServicos.SingleOrDefaultAsync(item => item.Id == id, cancelamento);

    public void Adicionar(TaxaServico taxa) => banco.TaxasServicos.Add(taxa);
}

// A F3 substituirá esta ausência pela consulta real aos contratos abertos.
public sealed class VinculosAluguelTaxaFase2 : IVinculosAluguelTaxa
{
    public Task<bool> PossuiAluguelAbertoAsync(Guid taxaId, CancellationToken cancelamento)
    {
        cancelamento.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
