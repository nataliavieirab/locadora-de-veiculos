namespace Locadora.Domain.Acesso;

public sealed class Empresa
{
    public Guid Id { get; private set; }

    private Empresa()
    {
    }

    public static Empresa Nova()
    {
        return new Empresa { Id = Guid.NewGuid() };
    }
}
