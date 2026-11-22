namespace Locadora.Application.Modulos.Acesso.Sessao;

public abstract record ResultadoLogin
{
    private ResultadoLogin() { }

    public sealed record Ok(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiraEm,
        Guid ContaId,
        Guid EmpresaId) : ResultadoLogin;

    public sealed record CredencialInvalida : ResultadoLogin
    {
        public static readonly CredencialInvalida Instancia = new();
    }

    public sealed record Indisponivel : ResultadoLogin
    {
        public static readonly Indisponivel Instancia = new();
    }
}

public abstract record ResultadoRenovacao
{
    private ResultadoRenovacao() { }

    public sealed record Ok(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiraEm,
        Guid ContaId,
        Guid EmpresaId) : ResultadoRenovacao;

    public sealed record Recusado : ResultadoRenovacao
    {
        public static readonly Recusado Instancia = new();
    }

    public sealed record Indisponivel : ResultadoRenovacao
    {
        public static readonly Indisponivel Instancia = new();
    }
}

public abstract record ResultadoLogout
{
    private ResultadoLogout() { }

    public sealed record Ok : ResultadoLogout
    {
        public static readonly Ok Instancia = new();
    }

    public sealed record Recusado : ResultadoLogout
    {
        public static readonly Recusado Instancia = new();
    }

    public sealed record Indisponivel : ResultadoLogout
    {
        public static readonly Indisponivel Instancia = new();
    }
}

public abstract record ResultadoConsulta
{
    private ResultadoConsulta() { }

    public sealed record Ok(Guid ContaId, Guid EmpresaId) : ResultadoConsulta;

    public sealed record Recusado : ResultadoConsulta
    {
        public static readonly Recusado Instancia = new();
    }

    public sealed record Indisponivel : ResultadoConsulta
    {
        public static readonly Indisponivel Instancia = new();
    }
}
