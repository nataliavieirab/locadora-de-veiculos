using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Locacoes;

public sealed class ConsultasContratacao(LocadoraDbContext banco) : IConsultasContratacao
{
    public async Task<DadosContratacao> CarregarAsync(ComandoContratacao comando, CancellationToken cancelamento)
    {
        var cliente = await banco.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.Id == comando.ClienteId, cancelamento)
            ?? throw new CadastroNaoEncontradoException();
        var condutor = await banco.Condutores.AsNoTracking().SingleOrDefaultAsync(c => c.Id == comando.CondutorId, cancelamento)
            ?? throw new CadastroNaoEncontradoException();
        var veiculo = await banco.Veiculos.AsNoTracking().SingleOrDefaultAsync(v => v.Id == comando.VeiculoId, cancelamento)
            ?? throw new CadastroNaoEncontradoException();
        var grupo = await banco.Grupos.AsNoTracking().SingleOrDefaultAsync(g => g.Id == veiculo.GrupoId, cancelamento)
            ?? throw new CadastroNaoEncontradoException();
        var plano = await banco.Planos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == comando.PlanoId, cancelamento)
            ?? throw new CadastroNaoEncontradoException();
        var taxas = await banco.TaxasServicos.AsNoTracking().Where(t => comando.TaxasServicoIds.Contains(t.Id)).ToListAsync(cancelamento);
        if (taxas.Count != comando.TaxasServicoIds.Count) throw new CadastroNaoEncontradoException();
        var seguros = await banco.PrecosSeguros.AsNoTracking().Where(s => comando.Seguros.Contains(s.Tipo)).ToListAsync(cancelamento);
        if (seguros.Count != comando.Seguros.Count)
            throw new ConflitoCadastroException("seguro_nao_configurado", "seguros");
        return new(cliente, condutor, veiculo, grupo, plano, comando.RetornoPrevisto, comando.HodometroInicial, taxas, seguros);
    }

    public Task<Aluguel?> AluguelAsync(Guid id, CancellationToken cancelamento)
        => banco.Alugueis.AsNoTracking().Include(a => a.Revisoes).ThenInclude(r => r.Itens)
            .Include(a => a.Revisoes).ThenInclude(r => r.Estimativa).ThenInclude(e => e.Componentes)
            .AsSplitQuery().SingleOrDefaultAsync(a => a.Id == id, cancelamento);

    public Task<bool> VeiculoOcupadoAsync(Guid veiculoId, Guid? excetoAluguelId, CancellationToken cancelamento)
        => banco.Alugueis.AnyAsync(a => a.VeiculoId == veiculoId && a.Estado == EstadoAluguel.EmAberto
            && a.Id != excetoAluguelId, cancelamento);

    public async Task<Pagina<Veiculo>> VeiculosAsync(Paginacao paginacao, Guid? grupoId, bool somenteDisponiveis,
        Guid? aluguelEmEdicaoId, CancellationToken cancelamento)
    {
        if (aluguelEmEdicaoId is Guid id)
        {
            var aluguel = await banco.Alugueis.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, cancelamento)
                ?? throw new CadastroNaoEncontradoException();
            if (aluguel.Estado != EstadoAluguel.EmAberto) throw new ConflitoCadastroException("aluguel_nao_editavel");
        }
        var consulta = banco.Veiculos.AsNoTracking().Where(v => grupoId == null || v.GrupoId == grupoId);
        if (somenteDisponiveis || aluguelEmEdicaoId is not null)
            consulta = consulta.Where(v => !banco.Alugueis.Any(a => a.VeiculoId == v.Id
                && a.Estado == EstadoAluguel.EmAberto && a.Id != aluguelEmEdicaoId));
        var total = await consulta.CountAsync(cancelamento);
        var itens = await consulta.OrderBy(v => v.PlacaNormalizada).ThenBy(v => v.Id)
            .Skip(paginacao.Deslocamento).Take(paginacao.PageSize).ToListAsync(cancelamento);
        return new(itens, paginacao.Page, paginacao.PageSize, total);
    }

    public async Task<Pagina<Condutor>> CondutoresAsync(Paginacao paginacao, Guid clienteId, CancellationToken cancelamento)
    {
        if (!await banco.Clientes.AnyAsync(c => c.Id == clienteId, cancelamento)) throw new CadastroNaoEncontradoException();
        var consulta = banco.Condutores.AsNoTracking().Where(c => c.ClienteId == clienteId);
        var total = await consulta.CountAsync(cancelamento);
        var itens = await consulta.OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Skip(paginacao.Deslocamento).Take(paginacao.PageSize).ToListAsync(cancelamento);
        return new(itens, paginacao.Page, paginacao.PageSize, total);
    }
}
