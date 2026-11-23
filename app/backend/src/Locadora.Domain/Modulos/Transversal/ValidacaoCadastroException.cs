namespace Locadora.Domain.Modulos.Transversal;

public sealed class ValidacaoCadastroException(string campo, string mensagem) : Exception(mensagem)
{
    public string Campo { get; } = campo;
}
