using Locadora.Application.Modulos.Locacoes;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Locacoes;

public sealed class RepositorioAlugueis(LocadoraDbContext banco) : IRepositorioAlugueis
{
    public Task<Aluguel?> ConsultarAsync(Guid id, CancellationToken cancelamento)
        => banco.Alugueis.Include(a => a.Revisoes).ThenInclude(r => r.Itens)
            .Include(a => a.Revisoes).ThenInclude(r => r.Estimativa).ThenInclude(e => e.Componentes)
            .AsSplitQuery().SingleOrDefaultAsync(a => a.Id == id, cancelamento);

    public Task<ComandoIdempotente?> ComandoAsync(Guid contaId, Guid chave, CancellationToken cancelamento)
        => banco.ComandosIdempotentes.AsNoTracking().SingleOrDefaultAsync(c =>
            c.ContaId == contaId && c.Chave == chave && c.Operacao == "criar-aluguel", cancelamento);

    public Task<RevisaoContrato> RevisaoAsync(Guid id, CancellationToken cancelamento)
        => banco.RevisoesContrato.AsNoTracking().Include(r => r.Itens).Include(r => r.Estimativa)
            .ThenInclude(e => e.Componentes).AsSplitQuery().SingleAsync(r => r.Id == id, cancelamento);

    public void Adicionar(Aluguel aluguel) => banco.Alugueis.Add(aluguel);
    public void Adicionar(ComandoIdempotente comando) => banco.ComandosIdempotentes.Add(comando);

    public async Task<byte[]> SalvarAsync(Aluguel aluguel, CancellationToken cancelamento)
    {
        if (banco.Database.CurrentTransaction is null)
            throw new InvalidOperationException("O contrato exige transação de empresa.");
        await banco.SaveChangesAsync(cancelamento);
        return aluguel.RowVersion.ToArray();
    }
}
