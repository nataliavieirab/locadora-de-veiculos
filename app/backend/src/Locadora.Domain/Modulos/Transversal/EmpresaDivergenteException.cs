namespace Locadora.Domain.Modulos.Transversal;

public sealed class EmpresaDivergenteException : InvalidOperationException
{
    public EmpresaDivergenteException()
        : base("A gravação não pertence à empresa da sessão.")
    {
    }
}
