using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Locacoes;

public sealed class ConsultasDevolucao(LocadoraDbContext banco, IRepositorioPrecosCombustiveis combustiveis,
    IContextoLocacao contexto) : IConsultasDevolucao
{
    public Task<Aluguel?> AluguelAsync(Guid id, CancellationToken cancelamento)
        => banco.Alugueis.AsNoTracking().Include(a => a.Revisoes).ThenInclude(r => r.Itens)
            .Include(a => a.Revisoes).ThenInclude(r => r.Estimativa).ThenInclude(e => e.Componentes)
            .AsSplitQuery().SingleOrDefaultAsync(a => a.Id == id, cancelamento);

    public async Task<DadosDevolucao> CarregarAsync(Aluguel aluguel, ComandoDevolucao comando, CancellationToken cancelamento,
        bool confirmarAceite = false)
    {
        if (aluguel.EmpresaId != contexto.EmpresaId) throw new CadastroNaoEncontradoException();
        var ids = comando.TaxasAdicionaisIds.ToArray();
        var taxas = await banco.TaxasServicos.AsNoTracking().Where(t => ids.Contains(t.Id)).OrderBy(t => t.Id).ToListAsync(cancelamento);
        if (taxas.Count != ids.Length)
        {
            if (!confirmarAceite) throw new CadastroNaoEncontradoException();
            foreach (var id in ids.Except(taxas.Select(t => t.Id)))
            {
                // SQL parametrizado e tenant explícito: não desliga filtros globais para distinguir taxa própria excluída.
                var excluida = await banco.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS [Value] FROM TaxasServicos WHERE EmpresaId = {contexto.EmpresaId} AND Id = {id} AND ExcluidoEmUtc IS NOT NULL")
                    .SingleAsync(cancelamento);
                if (excluida == 0) throw new CadastroNaoEncontradoException();
            }
            throw new ConflitoCadastroException("previa_desatualizada");
        }
        var preco = await combustiveis.AplicavelAsync(aluguel.RevisaoAtual.Veiculo.Combustivel,
            comando.RetornoReal, cancelamento) ?? throw new ConflitoCadastroException("combustivel_sem_vigencia", "retornoReal");
        return new(comando.RetornoReal, comando.HodometroFinal, comando.LitrosRestantes, preco, taxas);
    }

    public Task<byte[]?> VersaoAluguelAsync(Guid id, CancellationToken cancelamento)
        => banco.Alugueis.AsNoTracking().Where(a => a.Id == id).Select(a => a.RowVersion).SingleOrDefaultAsync(cancelamento);
}
