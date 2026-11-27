using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Locadora.ProtocoloFotos;

namespace Locadora.ProcessadorImagens;

internal static class ValidadorJpegEstrito
{
    static ValidadorJpegEstrito()
    {
        NativeLibrary.SetDllImportResolver(typeof(ValidadorJpegEstrito).Assembly, (nome, _, _) =>
        {
            if (nome != "libturbojpeg.so.0") return IntPtr.Zero;
            var caminho = Path.Combine(AppContext.BaseDirectory, "libturbojpeg.so.0");
            using var arquivo = File.OpenRead(caminho);
            // Binário Linux x64 extraído do .deb oficial cujo SHA-256 foi aprovado/qualificado.
            if (Convert.ToHexStringLower(SHA256.HashData(arquivo)) !=
                "dffb4dc98ac21a0bdb18ba5987b9ac97fb8ab6da92c9679add294c7cb50caa8f")
                throw new DllNotFoundException("Artefato nativo não qualificado.");
            return NativeLibrary.Load(caminho);
        });
    }

    // ABI pública TurboJPEG 3.2.1: não usar structs/callbacks/longjmp da API libjpeg.
    public static void Validar(byte[] bytes)
    {
        var handle = Nativo.tj3Init(1); // TJINIT_DECOMPRESS
        if (handle == IntPtr.Zero) throw new InvalidOperationException("Validador indisponível.");
        IntPtr pixels = IntPtr.Zero;
        try
        {
            if (Nativo.tj3Set(handle, 0, 1) != 0 // TJPARAM_STOPONWARNING
                || Nativo.tj3Set(handle, 24, (int)ProtocoloProcessador.MaximoPixels) != 0)
                throw new InvalidOperationException("Modo estrito indisponível.");
            if (Nativo.tj3DecompressHeader(handle, bytes, (nuint)bytes.Length) != 0) Rejeitar();
            var largura = Nativo.tj3Get(handle, 5); // TJPARAM_JPEGWIDTH
            var altura = Nativo.tj3Get(handle, 6); // TJPARAM_JPEGHEIGHT
            if (largura <= 0 || altura <= 0 || (long)largura * altura > ProtocoloProcessador.MaximoPixels)
                Rejeitar();
            var quantidade = checked(largura * altura * 4);
            pixels = Marshal.AllocHGlobal(quantidade);
            var cores = Nativo.tj3Get(handle, 8); // TJPARAM_COLORSPACE
            var formato = cores is 3 or 4 ? 11 : 7; // CMYK/YCCK -> TJPF_CMYK, demais -> TJPF_RGBA
            if (Nativo.tj3Decompress8(handle, bytes, (nuint)bytes.Length, pixels, checked(largura * 4), formato) != 0)
                Rejeitar();
        }
        finally
        {
            if (pixels != IntPtr.Zero) Marshal.FreeHGlobal(pixels);
            Nativo.tj3Destroy(handle);
        }
    }

    private static void Rejeitar() => throw new ImagemRejeitadaException(ProtocoloProcessador.Invalida);

    private static class Nativo
    {
        private const string Biblioteca = "libturbojpeg.so.0";
        [DllImport(Biblioteca, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr tj3Init(int tipo);
        [DllImport(Biblioteca, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int tj3Set(IntPtr handle, int parametro, int valor);
        [DllImport(Biblioteca, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int tj3Get(IntPtr handle, int parametro);
        [DllImport(Biblioteca, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int tj3DecompressHeader(IntPtr handle, [In] byte[] bytes, nuint tamanho);
        [DllImport(Biblioteca, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int tj3Decompress8(IntPtr handle, [In] byte[] bytes, nuint tamanho,
            IntPtr destino, int stride, int formato);
        [DllImport(Biblioteca, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void tj3Destroy(IntPtr handle);
    }
}
