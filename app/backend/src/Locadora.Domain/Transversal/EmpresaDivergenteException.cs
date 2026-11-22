namespace Locadora.Domain.Transversal;

public sealed class EmpresaDivergenteException : InvalidOperationException
{
    public EmpresaDivergenteException()
        : base("A gravação não pertence à empresa da sessão.")
    {
    }
}
