using System.Buffers.Binary;

namespace Locadora.ProtocoloFotos;

// Compartilhado como fonte pelo filho e pelo supervisor, sem referência ao executável.
internal static class ProtocoloProcessador
{
    public const int MaximoBytes = 5 * 1024 * 1024;
    public const long MaximoPixels = 16_000_000;
    public static ReadOnlySpan<byte> Pronto => "F5RD"u8;
    public const byte Iniciar = 42;
    public const byte Sucesso = 0, Invalida = 1, Formato = 2, Excesso = 3;

    public static void EscreverInteiro(Stream stream, int valor)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, valor);
        stream.Write(bytes);
    }

    public static async Task<int> LerInteiroAsync(Stream stream, CancellationToken token)
    {
        var bytes = new byte[4];
        await stream.ReadExactlyAsync(bytes, token);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }
}
