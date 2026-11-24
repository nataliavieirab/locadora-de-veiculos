using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Locacoes;

public sealed class ConsultasAluguel(LocadoraDbContext banco, IRepositorioFechamentoAluguel repositorio) : IConsultasAluguel
{
    public async Task<Pagina<ItemListaAluguel>> ListarAsync(Paginacao paginacao, FiltroConsultaAlugueis filtro,
        CancellationToken cancelamento)
    {
        var consulta = Raizes(filtro.IncluirOcultos);
        if (filtro.Estado is EstadoAluguel estado)
        {
            consulta = consulta.Where(a => a.Estado == estado);
        }

        if (filtro.VeiculoId is Guid veiculoId)
        {
            // A raiz/snapshot autorizado já limita a identidade histórica à empresa; frota ativa não é autoridade.
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
            .Select(a => new
            {
                a.Id, a.Estado, a.NumeroRevisaoAtual, a.SaidaUtc, a.RetornoPrevistoUtc,
                Oculto = a.ExcluidoEmUtc != null, TotalFinal = (decimal?)a.Devolucao!.TotalFinal,
                Revisao = a.Revisoes.Where(r => r.Numero == a.NumeroRevisaoAtual).Select(r => new
                {
                    r.Condutor.Nome, r.Condutor.Cpf, r.Veiculo.Placa, r.Plano.Modalidade, r.Estimativa.TotalConhecido,
                }).Single(),
            }).ToListAsync(cancelamento);
        var itens = pagina.Select(item => new ItemListaAluguel(item.Id, item.Estado, item.NumeroRevisaoAtual,
            item.Revisao.Nome, ResumoDocumento.Cpf(item.Revisao.Cpf), item.Revisao.Placa, item.Revisao.Modalidade.ToString(),
            item.SaidaUtc, item.RetornoPrevistoUtc, item.Revisao.TotalConhecido, item.TotalFinal, item.Oculto)).ToArray();
        return new(itens, paginacao.Page, paginacao.PageSize, total);
    }

    public Task<ResultadoConsultaAluguel> ConsultarAsync(Guid id, CancellationToken cancelamento)
        => ConsultarAsync(id, false, cancelamento);

    public async Task<ResultadoConsultaAluguel> ConsultarAsync(Guid id, bool incluirOcultos, CancellationToken cancelamento)
    {
        // Autorizar o pai antes de carregar filhos; remover somente o filtro de exclusão quando explícito.
        var autorizado = await Raizes(incluirOcultos).AnyAsync(a => a.Id == id, cancelamento);
        if (!autorizado) throw new CadastroNaoEncontradoException();
        var aluguel = await repositorio.ConsultarIncluindoOcultoAsync(id, cancelamento)
            ?? throw new CadastroNaoEncontradoException();
        if (!incluirOcultos && aluguel.ExcluidoEmUtc is not null) throw new CadastroNaoEncontradoException();
        return new(id, aluguel.RevisaoAtual, aluguel.Estado, aluguel.ExcluidoEmUtc is not null,
            aluguel.Devolucao, aluguel.RowVersion.ToArray());
    }

    public async Task<Pagina<ReferenciaVeiculoHistorico>> ReferenciasVeiculosAsync(Paginacao paginacao,
        bool incluirOcultos, CancellationToken cancelamento)
    {
        var snapshots = from aluguel in Raizes(incluirOcultos)
                        from revisao in aluguel.Revisoes
                        where revisao.Numero == aluguel.NumeroRevisaoAtual
                        select new { revisao.VeiculoId, revisao.Veiculo.Placa, revisao.Veiculo.Modelo, aluguel.SaidaUtc, aluguel.Id };
        // Subconsulta TOP(1) por identidade mantém deduplicação/paginação no SQL, sem materializar o histórico inteiro.
        var recentes = snapshots.Where(s => s.Id == snapshots.Where(outro => outro.VeiculoId == s.VeiculoId)
            .OrderByDescending(outro => outro.SaidaUtc).ThenBy(outro => outro.Id).Select(outro => outro.Id).First());
        var total = await recentes.CountAsync(cancelamento);
        var itens = await recentes
            .OrderBy(s => s.Placa).ThenBy(s => s.VeiculoId).Skip(paginacao.Deslocamento).Take(paginacao.PageSize)
            .Select(s => new ReferenciaVeiculoHistorico(s.VeiculoId, s.Placa, s.Modelo)).ToListAsync(cancelamento);
        return new(itens, paginacao.Page, paginacao.PageSize, total);
    }

    private IQueryable<Aluguel> Raizes(bool incluirOcultos)
    {
        var consulta = banco.Alugueis.AsNoTracking();
        if (incluirOcultos)
        {
            consulta = consulta.IgnoreQueryFilters(["ExclusaoLogicaAluguel"]);
        }
        return consulta;
    }
}
