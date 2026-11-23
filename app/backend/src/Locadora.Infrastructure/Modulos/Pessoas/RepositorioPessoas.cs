using Locadora.Application.Modulos.Pessoas;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Pessoas;

public sealed class RepositorioPessoas(LocadoraDbContext banco) : IRepositorioPessoas
{
    public async Task<Pagina<Cliente>> ListarClientesAsync(Paginacao paginacao, CancellationToken cancelamento)
    {
        var consulta = banco.Clientes.AsNoTracking();
        var total = await consulta.CountAsync(cancelamento);
        var itens = await consulta.OrderBy(item => item.Nome).ThenBy(item => item.Id)
            .Skip(paginacao.Deslocamento).Take(paginacao.PageSize).ToListAsync(cancelamento);
        return new Pagina<Cliente>(itens, paginacao.Page, paginacao.PageSize, total);
    }

    public async Task<Pagina<Condutor>> ListarCondutoresAsync(Paginacao paginacao, CancellationToken cancelamento)
    {
        var consulta = banco.Condutores.AsNoTracking();
        var total = await consulta.CountAsync(cancelamento);
        var itens = await consulta.OrderBy(item => item.Nome).ThenBy(item => item.Id)
            .Skip(paginacao.Deslocamento).Take(paginacao.PageSize).ToListAsync(cancelamento);
        return new Pagina<Condutor>(itens, paginacao.Page, paginacao.PageSize, total);
    }

    public Task<Cliente?> ClienteAsync(Guid id, CancellationToken cancelamento) =>
        banco.Clientes.SingleOrDefaultAsync(item => item.Id == id, cancelamento);

    public Task<Cliente?> ClientePorCpfAsync(string cpf, CancellationToken cancelamento) =>
        banco.Clientes.SingleOrDefaultAsync(item => item.CpfNormalizado == cpf, cancelamento);

    public Task<Condutor?> CondutorAsync(Guid id, CancellationToken cancelamento) =>
        banco.Condutores.SingleOrDefaultAsync(item => item.Id == id, cancelamento);

    public Task<Condutor?> CondutorPorCpfAsync(string cpf, CancellationToken cancelamento) =>
        banco.Condutores.SingleOrDefaultAsync(item => item.CpfNormalizado == cpf, cancelamento);

    public Task<bool> DocumentoClienteEmUsoAsync(TipoCliente tipo, string documento, Guid? excetoId, CancellationToken cancelamento) =>
        banco.Clientes.AnyAsync(item => item.Tipo == tipo && (excetoId == null || item.Id != excetoId)
            && (tipo == TipoCliente.PF ? item.CpfNormalizado == documento : item.CnpjNormalizado == documento), cancelamento);

    public Task<bool> CpfCondutorEmUsoAsync(string cpf, Guid? excetoId, CancellationToken cancelamento) =>
        banco.Condutores.AnyAsync(item => item.CpfNormalizado == cpf && (excetoId == null || item.Id != excetoId), cancelamento);

    public Task<bool> PossuiCondutoresAtivosAsync(Guid clienteId, CancellationToken cancelamento) =>
        banco.Condutores.AnyAsync(item => item.ClienteId == clienteId, cancelamento);

    public async Task<IReadOnlyList<VinculoCliente>> VinculosAsync(Guid clienteId, CancellationToken cancelamento) =>
        await banco.VinculosClientes.Where(item => item.ClientePfId == clienteId || item.ClientePjId == clienteId)
            .OrderBy(item => item.Id).ToListAsync(cancelamento);

    public Task<VinculoCliente?> VinculoAsync(Guid id, CancellationToken cancelamento) =>
        banco.VinculosClientes.SingleOrDefaultAsync(item => item.Id == id, cancelamento);

    public void Adicionar(Cliente cliente) => banco.Clientes.Add(cliente);
    public void Adicionar(Condutor condutor) => banco.Condutores.Add(condutor);
    public void Adicionar(VinculoCliente vinculo) => banco.VinculosClientes.Add(vinculo);
}
