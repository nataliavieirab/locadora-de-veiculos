using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.FrotaPrecos;

/// <summary>
/// Mantém uma configuração corrente por tipo de seguro. Ausência de configuração é
/// representada por <c>null</c> e não se confunde com o preço zero explícito.
/// </summary>
public sealed class CasosPrecosSeguros(IRepositorioPrecosSeguros repositorio, IUnidadeTrabalhoEmpresa unidade,
    IAuditoriaCadastro auditoria, IContextoCadastro contexto, TimeProvider tempo)
{
    public Task<IReadOnlyList<PrecoSeguro>> ListarAsync(CancellationToken cancelamento) =>
        repositorio.ListarSegurosAsync(cancelamento);

    public Task<PrecoSeguro?> SeguroAsync(TipoSeguro tipo, CancellationToken cancelamento) =>
        repositorio.SeguroAsync(tipo, cancelamento);

    public Task<PrecoSeguro> SalvarAsync(TipoSeguro tipo, decimal precoDiario, string? versao,
        CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var atual = await repositorio.SeguroAsync(tipo, token);
            if (atual is null)
            {
                if (!string.IsNullOrWhiteSpace(versao)) throw new PrecondicaoCadastroException(ausente: false);
                var novo = PrecoSeguro.Novo(contexto.EmpresaId, tipo, precoDiario, tempo.GetUtcNow());
                repositorio.Adicionar(novo);
                auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.PrecoSeguro, novo.Id, "tipo", "precoDiario");
                return novo;
            }

            VersaoCadastro.Exigir(versao, atual.RowVersion);
            if (atual.PrecoDiario != precoDiario)
            {
                atual.Alterar(precoDiario, tempo.GetUtcNow());
                auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.PrecoSeguro, atual.Id, "precoDiario");
            }

            return atual;
        }, cancelamento);
}
