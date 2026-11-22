namespace Locadora.Application.Modulos.Acesso.Registro;

public interface IConsultaUsername
{
    Task<bool> ExisteNormalizadoAsync(string usernameNormalizado, CancellationToken cancelamento);
}

public interface IRegistroEmpresa
{
    Task<ResultadoRegistro> RegistrarAsync(string username, string email, string password, CancellationToken cancelamento);
}

public interface IGanchoRegistro
{
    Task DepoisDaContaAsync(CancellationToken cancelamento);
}

public sealed class GanchoRegistroNulo : IGanchoRegistro
{
    public Task DepoisDaContaAsync(CancellationToken cancelamento) => Task.CompletedTask;
}

public sealed class CasoRegistroEmpresa
{
    private readonly IConsultaUsername _consulta;
    private readonly IRegistroEmpresa _registro;

    public CasoRegistroEmpresa(IConsultaUsername consulta, IRegistroEmpresa registro)
    {
        _consulta = consulta;
        _registro = registro;
    }

    public async Task<ResultadoRegistro> ExecutarAsync(
        string? username,
        string? email,
        string? password,
        CancellationToken cancelamento)
    {
        var erros = ValidadorRegistro.Validar(username, email, password);
        if (erros.Count > 0)
        {
            return new ResultadoRegistro.Invalido(erros);
        }

        var usuario = username!.Trim();
        var correio = email!.Trim();
        var senha = password!;
        var normalizado = NormalizadorUsuario.Normalizar(usuario);
        if (await _consulta.ExisteNormalizadoAsync(normalizado, cancelamento))
        {
            return new ResultadoRegistro.Conflito("username", "Já existe uma conta com este usuário.");
        }

        return await _registro.RegistrarAsync(usuario, correio, senha, cancelamento);
    }
}
