using System.Globalization;
using System.Text.RegularExpressions;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Api.Host;

public static partial class DecimalTransporte
{
    public static decimal Ler(string? valor, string campo)
    {
        if (valor is null || !Formato().IsMatch(valor)
            || !decimal.TryParse(valor, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var numero))
        {
            throw new ValidacaoCadastroException(campo, "Informe um decimal em string com ponto, sem expoente ou separador de milhares.");
        }
        return numero;
    }

    public static decimal? Opcional(string? valor, string campo) => valor is null ? null : Ler(valor, campo);
    public static string Escrever(decimal valor) => valor.ToString("0.00####", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^-?[0-9]+(?:\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}
