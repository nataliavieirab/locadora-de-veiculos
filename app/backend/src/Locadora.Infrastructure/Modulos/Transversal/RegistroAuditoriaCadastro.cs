using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;

namespace Locadora.Infrastructure.Modulos.Transversal;

public sealed class RegistroAuditoriaCadastro(
    LocadoraDbContext banco, ContextoEmpresa contexto, TimeProvider tempo) : IAuditoriaCadastro
{
    public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid entidadeId, params string[] campos)
    {
        if (contexto.EmpresaId is not Guid empresaId || contexto.ContaId is not Guid contaId
            || contexto.CorrelacaoId is not Guid correlacaoId)
        {
            throw new EmpresaDivergenteException();
        }

        if (banco.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A auditoria deve participar da transação cadastral.");
        }

        banco.AuditoriasCadastro.Add(AuditoriaCadastro.Registrar(
            empresaId, contaId, tempo.GetUtcNow(), acao, entidade, entidadeId, correlacaoId, campos));
    }
}
