namespace Locadora.Api.Modulos.Acesso.Registro;

public sealed class EntradaRegistro
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public Guid? EmpresaId { get; set; }
}
