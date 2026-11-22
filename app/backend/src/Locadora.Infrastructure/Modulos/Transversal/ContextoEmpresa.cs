namespace Locadora.Infrastructure.Modulos.Transversal;

public sealed class ContextoEmpresa
{
    public Guid? EmpresaId { get; set; }

    public bool PermiteCriarEmpresa { get; set; }
}
