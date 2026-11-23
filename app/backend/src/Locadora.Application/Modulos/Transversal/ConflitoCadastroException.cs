namespace Locadora.Application.Modulos.Transversal;

public sealed class ConflitoCadastroException(string codigo, string? campo = null) : Exception("Um vínculo ou registro existente impede a operação.")
{
    public string Codigo { get; } = codigo;
    public string? Campo { get; } = campo;
}
