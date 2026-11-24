using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Locadora.Application.Modulos.Locacoes;

public static class HashDevolucao
{
    public static string Calcular(Guid aluguelId, ComandoConclusaoDevolucao comando, string? versaoOriginal)
    {
        var canonico = new
        {
            Operacao = "devolver-aluguel", Aluguel = aluguelId, VersaoOriginal = versaoOriginal?.Trim(),
            Retorno = comando.Dados.RetornoReal.UtcTicks, comando.Dados.HodometroFinal,
            Litros = comando.Dados.LitrosRestantes.ToString("G29", CultureInfo.InvariantCulture),
            Taxas = comando.Dados.TaxasAdicionaisIds.Order().ToArray(),
            comando.VersoesAceitas.RevisaoId, comando.VersoesAceitas.VersaoRegra, comando.VersoesAceitas.CombustivelId,
            TaxasAceitas = comando.VersoesAceitas.TaxasNovas.OrderBy(t => t.Id).ToArray(),
            comando.VersoesAceitas.ResumoCalculo,
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonico)));
    }
}
