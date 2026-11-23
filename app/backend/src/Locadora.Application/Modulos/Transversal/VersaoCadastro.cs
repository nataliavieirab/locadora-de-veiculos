namespace Locadora.Application.Modulos.Transversal;

public sealed class PrecondicaoCadastroException(bool ausente) : Exception("Recarregue o cadastro antes de confirmar a alteração.")
{
    public bool Ausente { get; } = ausente;
}

public static class VersaoCadastro
{
    public static string Etag(byte[] rowVersion)
    {
        if (rowVersion.Length != 8)
        {
            throw new InvalidOperationException("A versão deve ser obtida do SQL Server.");
        }

        return $"\"{Convert.ToBase64String(rowVersion)}\"";
    }

    public static void Exigir(string? ifMatch, byte[] rowVersion)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new PrecondicaoCadastroException(ausente: true);
        }

        // Somente a versão forte emitida pela API evita sobrescrita de uma tela antiga.
        if (!string.Equals(ifMatch.Trim(), Etag(rowVersion), StringComparison.Ordinal))
        {
            throw new PrecondicaoCadastroException(ausente: false);
        }
    }
}
