namespace Locadora.Application.Modulos.Transversal;

public sealed class ArmazenamentoIndisponivelException : Exception
{
    public ArmazenamentoIndisponivelException()
        : base("O armazenamento está indisponível.")
    {
    }
}
