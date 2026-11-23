using System.Net.Mail;

namespace Locadora.Domain.Modulos.Transversal;

public static class ValidacaoCadastro
{
    public static string Texto(string? valor, string campo, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length > maximo)
        {
            throw new ValidacaoCadastroException(campo, $"Informe um valor com até {maximo} caracteres.");
        }

        return valor.Trim();
    }

    public static string? Email(string? valor, bool obrigatorio = false)
    {
        if (!obrigatorio && string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var texto = Texto(valor, "email", 254);
        if (!MailAddress.TryCreate(texto, out var endereco) || endereco.Address != texto)
        {
            throw new ValidacaoCadastroException("email", "Informe um e-mail válido.");
        }

        return texto;
    }

    public static decimal Preco(decimal valor, string campo)
    {
        if (valor < 0 || valor > 9999999999999.999999m || Escala(valor) > 6)
        {
            throw new ValidacaoCadastroException(campo, "Informe um preço não negativo, com até seis casas decimais e treze dígitos inteiros.");
        }

        return valor;
    }

    public static int Escala(decimal valor) => (decimal.GetBits(valor)[3] >> 16) & 0xff;

    public static Guid Referencia(Guid id, string campo)
    {
        if (id == Guid.Empty)
        {
            throw new ValidacaoCadastroException(campo, "Selecione um registro válido.");
        }

        return id;
    }

    public static T Modalidade<T>(T valor, string campo) where T : struct, Enum
    {
        if (!Enum.IsDefined(valor))
        {
            throw new ValidacaoCadastroException(campo, "Selecione uma opção válida.");
        }

        return valor;
    }
}
