using System.Security.Cryptography;
using System.Text.Json;

namespace Locadora.Application.Modulos.Locacoes;

public static class HashContratacao
{
    public static string Calcular(ComandoContratacao comando)
    {
        comando.Validar();
        var canonico = new
        {
            Operacao = "criar-aluguel",
            Recurso = "/api/v1/alugueis",
            ClienteId = comando.ClienteId.ToString("N"),
            CondutorId = comando.CondutorId.ToString("N"),
            VeiculoId = comando.VeiculoId.ToString("N"),
            PlanoId = comando.PlanoId.ToString("N"),
            RetornoPrevistoUtc = comando.RetornoPrevisto.UtcTicks,
            comando.HodometroInicial,
            TaxasServicoIds = comando.TaxasServicoIds.Select(id => id.ToString("N")).Order(StringComparer.Ordinal).ToArray(),
            Seguros = comando.Seguros.Select(tipo => tipo.ToString()).Order(StringComparer.Ordinal).ToArray(),
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonico)));
    }
}
