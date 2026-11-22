namespace Locadora.Application.Modulos.Acesso.Registro;

public sealed record ErroCampo(string Campo, string Mensagem);

public abstract record ResultadoRegistro
{
    private ResultadoRegistro() { }

    public sealed record Criado(Guid EmpresaId, Guid ContaId) : ResultadoRegistro;

    public sealed record Invalido(IReadOnlyList<ErroCampo> Erros) : ResultadoRegistro;

    public sealed record Conflito(string Campo, string Mensagem) : ResultadoRegistro;
}

public static class CodigoResultadoRegistro
{
    public static int ParaHttp(ResultadoRegistro resultado) => resultado switch
    {
        ResultadoRegistro.Criado => 201,
        ResultadoRegistro.Invalido => 400,
        ResultadoRegistro.Conflito => 409,
        _ => 500,
    };
}
