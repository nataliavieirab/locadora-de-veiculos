namespace Locadora.Application.Modulos.Locacoes;

public static class ResumoDocumento
{
    public static string Cpf(string valor)
    {
        var digitos = new string(valor.Where(char.IsDigit).ToArray());
        return digitos.Length >= 2 ? $"***.***.***-{digitos[^2..]}" : "***";
    }
}