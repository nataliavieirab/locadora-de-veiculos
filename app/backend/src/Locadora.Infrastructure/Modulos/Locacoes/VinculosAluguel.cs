using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Pessoas;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Locacoes;

public sealed class VinculosAluguelPessoas(LocadoraDbContext banco) : IVinculosAluguelPessoas
{
    public Task<bool> ClientePossuiAluguelAbertoAsync(Guid clienteId, CancellationToken cancelamento)
        => banco.Alugueis.AnyAsync(a => a.Estado == EstadoAluguel.EmAberto && a.ClienteId == clienteId, cancelamento);

    public Task<bool> CondutorPossuiAluguelAbertoAsync(Guid condutorId, CancellationToken cancelamento)
        => banco.Alugueis.AnyAsync(a => a.Estado == EstadoAluguel.EmAberto && a.CondutorId == condutorId, cancelamento);
}

public sealed class VinculosAluguelPlano(LocadoraDbContext banco) : IVinculosAluguelPlano
{
    public Task<bool> PlanoFoiContratadoAsync(Guid planoId, CancellationToken cancelamento)
        => banco.RevisoesContrato.AnyAsync(r => r.PlanoId == planoId, cancelamento);
}

public sealed class VinculosAluguelVeiculo(LocadoraDbContext banco) : IVinculosAluguelVeiculo
{
    public Task<bool> PossuiAluguelAbertoAsync(Guid veiculoId, CancellationToken cancelamento)
        => banco.Alugueis.AnyAsync(a => a.Estado == EstadoAluguel.EmAberto && a.VeiculoId == veiculoId, cancelamento);
}

public sealed class VinculosAluguelTaxa(LocadoraDbContext banco) : IVinculosAluguelTaxa
{
    public Task<bool> PossuiAluguelAbertoAsync(Guid taxaId, CancellationToken cancelamento)
        => (from i in banco.ItensContratados.AsNoTracking()
            where i.TaxaServicoId == taxaId
            join r in banco.RevisoesContrato.AsNoTracking() on i.RevisaoId equals r.Id
            join a in banco.Alugueis.AsNoTracking() on r.AluguelId equals a.Id
            where a.Estado == EstadoAluguel.EmAberto && r.Numero == a.NumeroRevisaoAtual
            select i).AnyAsync(cancelamento);
}