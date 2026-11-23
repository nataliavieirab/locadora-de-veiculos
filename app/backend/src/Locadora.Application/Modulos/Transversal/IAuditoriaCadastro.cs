using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Transversal;

public interface IAuditoriaCadastro
{
    void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid entidadeId, params string[] campos);
}
