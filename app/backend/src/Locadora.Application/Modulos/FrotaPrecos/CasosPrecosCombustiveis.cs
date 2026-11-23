using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.FrotaPrecos;

/// <summary>
/// Preços de combustível são append-only: cada registro cria uma nova vigência com
/// instante atribuído pelo servidor, sem editar ou excluir versões anteriores.
/// </summary>
public sealed class CasosPrecosCombustiveis(IRepositorioPrecosCombustiveis repositorio,
    IUnidadeTrabalhoEmpresa unidade, IAuditoriaCadastro auditoria, IContextoCadastro contexto, TimeProvider tempo)
{
    public Task<IReadOnlyList<PrecoCombustivel>> ListarAsync(TipoCombustivel? tipo, CancellationToken cancelamento) =>
        repositorio.ListarAsync(tipo, cancelamento);

    public Task<PrecoCombustivel?> VigenteAsync(TipoCombustivel tipo, CancellationToken cancelamento) =>
        repositorio.VigenteAsync(tipo, cancelamento);

    /// <summary>
    /// Seleciona a última vigência iniciada até o momento informado. Retorna
    /// <c>null</c> quando nenhuma configuração é aplicável, sem inventar preço.
    /// </summary>
    public Task<PrecoCombustivel?> AplicavelAsync(TipoCombustivel tipo, DateTimeOffset momento,
        CancellationToken cancelamento) =>
        repositorio.AplicavelAsync(tipo, momento.ToUniversalTime(), cancelamento);

    public Task<PrecoCombustivel> RegistrarAsync(TipoCombustivel tipo, decimal precoUnitario,
        CancellationToken cancelamento) =>
        unidade.ExecutarAsync(token =>
        {
            var preco = PrecoCombustivel.Novo(contexto.EmpresaId, tipo, precoUnitario, tempo.GetUtcNow());
            repositorio.Adicionar(preco);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.PrecoCombustivel, preco.Id,
                "tipo", "precoUnitario", "inicioVigenciaUtc");
            return Task.FromResult(preco);
        }, cancelamento);
}
