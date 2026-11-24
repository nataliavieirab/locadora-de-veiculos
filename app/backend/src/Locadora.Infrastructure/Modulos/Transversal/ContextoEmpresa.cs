using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Application.Modulos.Locacoes;

namespace Locadora.Infrastructure.Modulos.Transversal;

public sealed class ContextoEmpresa : IContextoCadastro, IContextoLocacao
{
    public Guid? EmpresaId { get; set; }
    public Guid? ContaId { get; set; }
    public Guid? CorrelacaoId { get; set; }

    public bool PermiteCriarEmpresa { get; set; }

    Guid IContextoCadastro.EmpresaId => EmpresaId is Guid id && id != Guid.Empty
        ? id : throw new EmpresaDivergenteException();
    Guid IContextoLocacao.ContaId => ContaId is Guid id && id != Guid.Empty
        ? id : throw new EmpresaDivergenteException();
}
