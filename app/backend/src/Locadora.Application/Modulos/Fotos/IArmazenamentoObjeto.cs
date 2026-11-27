using System.Security.Cryptography;
using Locadora.Domain.Modulos.Fotos;

namespace Locadora.Application.Modulos.Fotos;

// Destino sempre obtido da configuração e da pendência persistida, nunca do HTTP.
public sealed record ObjetoFoto(string Chave, string Mime, int TamanhoBytes, byte[] Sha256)
{
    public void Validar()
    {
        var partes = Chave.Split('/');
        if (partes.Length != 2 || partes.Any(p => p.Length != 32
            || !Guid.TryParseExact(p, "N", out var id) || id == Guid.Empty)
            || Mime is not ("image/png" or "image/jpeg") || TamanhoBytes is <= 0 or > FotoObjeto.MaximoBytes
            || Sha256.Length != 32)
            throw new ArmazenamentoObjetoException();
    }

    public void Conferir(ReadOnlySpan<byte> bytes)
    {
        Validar();
        if (bytes.Length != TamanhoBytes || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Sha256))
            throw new ArmazenamentoObjetoException();
    }
}

public sealed class ArmazenamentoObjetoException() : Exception("Armazenamento da foto indisponível.");

public interface IArmazenamentoObjeto
{
    Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken cancelamento);
    // Leitura limitada, íntegra e desacoplada do stream remoto, que é fechado no adaptador.
    Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken cancelamento);
    Task RemoverAsync(ObjetoFoto objeto, CancellationToken cancelamento);
    Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken cancelamento);
}
