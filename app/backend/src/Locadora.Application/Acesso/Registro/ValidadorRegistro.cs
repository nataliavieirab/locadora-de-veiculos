using System.Net.Mail;

namespace Locadora.Application.Acesso.Registro;

public static class NormalizadorUsuario
{
    public static string Normalizar(string username) => username.Trim().ToUpperInvariant();
}

public static class ValidadorRegistro
{
    public const int UsuarioMaximo = 256;
    public const int EmailMaximo = 256;
    public const int SenhaMinima = 8;
    public const int SenhaMaxima = 128;

    public static IReadOnlyList<ErroCampo> Validar(string? username, string? email, string? password)
    {
        var erros = new List<ErroCampo>();
        ValidarUsuario(username, erros);
        ValidarEmail(email, erros);
        ValidarSenha(password, erros);
        return erros;
    }

    private static void ValidarUsuario(string? username, List<ErroCampo> erros)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            erros.Add(new ErroCampo("username", "Informe o usuário."));
            return;
        }

        var aparado = username.Trim();
        if (aparado.Length == 0)
        {
            erros.Add(new ErroCampo("username", "Informe o usuário."));
            return;
        }

        if (aparado.Any(char.IsWhiteSpace))
        {
            erros.Add(new ErroCampo("username", "O usuário não pode conter espaços."));
            return;
        }

        if (aparado.Length > UsuarioMaximo)
        {
            erros.Add(new ErroCampo("username", "O usuário pode ter no máximo 256 caracteres."));
        }
    }

    private static void ValidarEmail(string? email, List<ErroCampo> erros)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            erros.Add(new ErroCampo("email", "Informe o e-mail."));
            return;
        }

        var aparado = email.Trim();
        if (aparado.Length > EmailMaximo)
        {
            erros.Add(new ErroCampo("email", "O e-mail pode ter no máximo 256 caracteres."));
            return;
        }

        if (!EmailValido(aparado))
        {
            erros.Add(new ErroCampo("email", "Informe um e-mail válido."));
        }
    }

    private static void ValidarSenha(string? password, List<ErroCampo> erros)
    {
        if (string.IsNullOrEmpty(password) || password.Length < SenhaMinima)
        {
            erros.Add(new ErroCampo("password", "A senha precisa ter pelo menos 8 caracteres."));
            return;
        }

        if (password.Length > SenhaMaxima)
        {
            erros.Add(new ErroCampo("password", "A senha pode ter no máximo 128 caracteres."));
        }
    }

    private static bool EmailValido(string email)
    {
        if (email.Any(char.IsWhiteSpace) || !email.Contains('@'))
        {
            return false;
        }

        try
        {
            var endereco = new MailAddress(email);
            return endereco.Address == email
                && endereco.Host.Contains('.')
                && !endereco.Host.StartsWith('.')
                && !endereco.Host.EndsWith('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
