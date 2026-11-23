using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Pessoas;

namespace Locadora.Application.Modulos.Pessoas;

public interface IRepositorioPessoas
{
    Task<Pagina<Cliente>> ListarClientesAsync(Paginacao paginacao, CancellationToken cancelamento);
    Task<Pagina<Condutor>> ListarCondutoresAsync(Paginacao paginacao, CancellationToken cancelamento);
    Task<Cliente?> ClienteAsync(Guid id, CancellationToken cancelamento);
    Task<Cliente?> ClientePorCpfAsync(string cpf, CancellationToken cancelamento);
    Task<Condutor?> CondutorAsync(Guid id, CancellationToken cancelamento);
    Task<Condutor?> CondutorPorCpfAsync(string cpf, CancellationToken cancelamento);
    Task<bool> DocumentoClienteEmUsoAsync(TipoCliente tipo, string documento, Guid? excetoId, CancellationToken cancelamento);
    Task<bool> CpfCondutorEmUsoAsync(string cpf, Guid? excetoId, CancellationToken cancelamento);
    Task<bool> PossuiCondutoresAtivosAsync(Guid clienteId, CancellationToken cancelamento);
    Task<IReadOnlyList<VinculoCliente>> VinculosAsync(Guid clienteId, CancellationToken cancelamento);
    Task<VinculoCliente?> VinculoAsync(Guid id, CancellationToken cancelamento);
    void Adicionar(Cliente cliente);
    void Adicionar(Condutor condutor);
    void Adicionar(VinculoCliente vinculo);
}

public interface IVinculosAluguelPessoas
{
    Task<bool> ClientePossuiAluguelAbertoAsync(Guid clienteId, CancellationToken cancelamento);
    Task<bool> CondutorPossuiAluguelAbertoAsync(Guid condutorId, CancellationToken cancelamento);
}
