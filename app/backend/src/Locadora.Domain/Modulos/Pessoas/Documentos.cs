using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Pessoas;

public static class Documentos
{
    public static string Cpf(string? valor)
    {
        var normalizado = Normalizar(valor, "cpf");
        if (normalizado.Length != 11 || normalizado.Any(caractere => !char.IsAsciiDigit(caractere))
            || normalizado.Distinct().Count() == 1)
        {
            throw new ValidacaoCadastroException("cpf", "Informe um CPF válido.");
        }

        for (var tamanho = 9; tamanho <= 10; tamanho++)
        {
            var soma = 0;
            for (var indice = 0; indice < tamanho; indice++)
            {
                soma += (normalizado[indice] - '0') * (tamanho + 1 - indice);
            }

            var resto = soma % 11;
            if (normalizado[tamanho] - '0' != (resto < 2 ? 0 : 11 - resto))
            {
                throw new ValidacaoCadastroException("cpf", "Informe um CPF válido.");
            }
        }

        return normalizado;
    }

    public static string Cnpj(string? valor)
    {
        var normalizado = Normalizar(valor, "cnpj");
        if (normalizado.Length != 14 || normalizado[..12].Any(caractere => !char.IsAsciiLetterOrDigit(caractere))
            || !char.IsAsciiDigit(normalizado[12]) || !char.IsAsciiDigit(normalizado[13])
            || normalizado.Distinct().Count() == 1)
        {
            throw new ValidacaoCadastroException("cnpj", "Informe um CNPJ válido.");
        }

        // Receita Federal: módulo 11, valor ASCII menos 48; aceita também o formato numérico.
        for (var tamanho = 12; tamanho <= 13; tamanho++)
        {
            var soma = 0;
            for (var indice = tamanho - 1; indice >= 0; indice--)
            {
                soma += (normalizado[indice] - '0') * (2 + (tamanho - 1 - indice) % 8);
            }

            var resto = soma % 11;
            if (normalizado[tamanho] - '0' != (resto < 2 ? 0 : 11 - resto))
            {
                throw new ValidacaoCadastroException("cnpj", "Informe um CNPJ válido.");
            }
        }

        return normalizado;
    }

    public static string Cnh(string? valor)
    {
        var texto = ValidacaoCadastro.Texto(valor, "cnh", 64);
        var normalizado = string.Concat(texto.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
        return ValidacaoCadastro.Texto(normalizado, "cnh", 64);
    }

    private static string Normalizar(string? valor, string campo)
    {
        var texto = ValidacaoCadastro.Texto(valor, campo, 64);
        if (texto.Any(caractere => !char.IsAsciiLetterOrDigit(caractere) && caractere is not ('.' or '/' or '-' or ' ')))
        {
            throw new ValidacaoCadastroException(campo, "Informe um documento válido.");
        }

        return string.Concat(texto.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
    }
}
