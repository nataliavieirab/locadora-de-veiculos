using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Locacoes;

public sealed class ConsultasAluguel(LocadoraDbContext banco, IRepositorioAlugueis repositorio) : IConsultasAluguel
{
    public async Task<Pagina<ItemListaAluguel>> ListarAsync(Paginacao paginacao, FiltroConsultaAlugueis filtro,
        CancellationToken cancelamento)
    {
        var consulta = banco.Alugueis.AsNoTracking().AsQueryable();
        if (filtro.Estado is EstadoAluguel estado)
        {
            consulta = consulta.Where(a => a.Estado == estado);
        }

        if (filtro.VeiculoId is Guid veiculoId)
        {
            if (!await banco.Veiculos.AnyAsync(v => v.Id == veiculoId, cancelamento))
            {
                return new([], paginacao.Page, paginacao.PageSize, 0);
            }

            consulta = consulta.Where(a => a.VeiculoId == veiculoId);
        }

        if (filtro.SaidaDe is DateTimeOffset de)
        {
            consulta = consulta.Where(a => a.SaidaUtc >= de);
        }

        if (filtro.SaidaAte is DateTimeOffset ate)
        {
            consulta = consulta.Where(a => a.SaidaUtc < ate);
        }

        var total = await consulta.CountAsync(cancelamento);
        var pagina = await consulta.OrderByDescending(a => a.SaidaUtc).ThenBy(a => a.Id)
            .Skip(paginacao.Deslocamento).Take(paginacao.PageSize)
            .Select(a => new { a.Id, a.NumeroRevisaoAtual }).ToListAsync(cancelamento);
        if (pagina.Count == 0)
        {
            return new([], paginacao.Page, paginacao.PageSize, total);
        }

        var ids = pagina.Select(item => item.Id).ToList();
        var alugueis = await banco.Alugueis.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(cancelamento);
        var numeros = pagina.ToDictionary(item => item.Id, item => item.NumeroRevisaoAtual);
        var revisoes = await banco.RevisoesContrato.AsNoTracking().Include(r => r.Estimativa)
            .Where(r => ids.Contains(r.AluguelId))
            .ToListAsync(cancelamento);
        var ordem = pagina.Select(item => item.Id).ToList();
        var itens = ordem.Select(id =>
        {
            var aluguel = alugueis.Single(a => a.Id == id);
            var revisao = revisoes.Single(r => r.AluguelId == id && r.Numero == numeros[id]);
            return ItemListaAluguel.De(aluguel, revisao);
        }).ToList();
        return new(itens, paginacao.Page, paginacao.PageSize, total);
    }

    public async Task<ResultadoContratacao> ConsultarAsync(Guid id, CancellationToken cancelamento)
    {
        var aluguel = await repositorio.ConsultarAsync(id, cancelamento)
            ?? throw new CadastroNaoEncontradoException();
        return new(id, aluguel.RevisaoAtual, aluguel.RowVersion.ToArray());
    }
}