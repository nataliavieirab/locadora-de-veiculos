using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Ensaios.FotosR2.Retencao;
using Locadora.Infrastructure.Modulos.Fotos;

namespace Locadora.Ensaios.FotosR2.LeituraAusente;

// Abre somente o manifesto aprovado, sem construtor de retenção/persistência/SQL.
[SupportedOSPlatform("linux")]
internal sealed class PermissaoLeituraAusente
{
    internal const string PastaAprovada = "r2-retencao-ceffa25bc9224931926098c224ae8b63";
    private const UnixFileMode ArquivoPrivado = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PastaPrivada = ArquivoPrivado | UnixFileMode.UserExecute;
    private readonly string caminho;
    private readonly byte[] original;
    private readonly string chave;
    private readonly HashSet<string> antigas;
    private int operacao;
    private int encaminhamentos;
    public Uri Endpoint { get; }
    internal string Chave => chave;
    internal int Encaminhamentos => Volatile.Read(ref encaminhamentos);

    private PermissaoLeituraAusente(string backend)
    {
        caminho = Path.Combine(Path.GetFullPath(backend), "obj", PastaAprovada, "evidencias.json");
        original = LerPrivado();
        using var json = JsonDocument.Parse(original);
        var raiz = json.RootElement;
        ConferirDuplicadas(raiz);
        if (raiz.GetProperty("bucket").GetString() != ManifestoRetencao.Bucket) Negar();
        Endpoint = new Uri(raiz.GetProperty("endpoint").GetString()!);
        new ConfiguracaoR2
        {
            Endpoint = Endpoint.AbsoluteUri, Bucket = ManifestoRetencao.Bucket,
            BucketPrivadoConfirmado = true, AccessKey = "offline", SecretKey = "offline",
        }.Validar();
        if (Endpoint.OriginalString.TrimEnd('/') != Endpoint.GetLeftPart(UriPartial.Authority)) Negar();
        var catalogo = raiz.GetProperty("catalogo").GetString();
        var protegidos = raiz.GetProperty("catalogosProtegidos").EnumerateArray().Select(x => x.GetString()).ToArray();
        if (!ManifestoRetencao.CatalogoValido(catalogo) || protegidos.Length != 2
            || protegidos.Any(x => !ManifestoRetencao.CatalogoValido(x))
            || protegidos.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2
            || protegidos.Contains(catalogo, StringComparer.OrdinalIgnoreCase)) Negar();
        var negadas = raiz.GetProperty("denylist").EnumerateArray().Select(x => x.GetString()!).ToArray();
        antigas = new(negadas, StringComparer.Ordinal);
        if (negadas.Length != 5 || antigas.Count != 5) Negar();
        foreach (var antiga in antigas) ManifestoRetencao.ChaveCanonica(antiga);
        var empresas = raiz.GetProperty("empresas").EnumerateArray().Select(x => x.GetGuid()).ToArray();
        if (empresas.Length != 1 || empresas[0] == Guid.Empty
            || antigas.Any(x => x.Split('/')[0] == empresas[0].ToString("N"))) Negar();
        var objetos = raiz.GetProperty("objetos").EnumerateArray().ToArray();
        if (objetos.Length != 3) Negar();
        var chaves = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in objetos)
        {
            var atual = item.GetProperty("Chave").GetString()!;
            ManifestoRetencao.ChaveCanonica(atual);
            var objeto = new ObjetoFoto(atual, item.GetProperty("Mime").GetString()!,
                item.GetProperty("TamanhoBytes").GetInt32(), Convert.FromHexString(item.GetProperty("Sha256").GetString()!));
            objeto.Validar();
            var estado = (EstadoFoto)item.GetProperty("Estado").GetInt32();
            var tipo = (TipoTrabalhoObjeto)item.GetProperty("Tipo").GetInt32();
            if (!chaves.Add(atual) || antigas.Contains(atual)
                || atual.Split('/')[0] != empresas[0].ToString("N")
                || item.GetProperty("Empresa").GetGuid() != empresas[0]
                || item.GetProperty("FotoId").GetGuid().ToString("N") != atual.Split('/')[1]
                || item.GetProperty("TrabalhoId").GetGuid() == Guid.Empty
                || !item.GetProperty("SemReferenciasProtegidas").GetBoolean()
                || item.GetProperty("EstadoTrabalho").GetInt32() != (int)EstadoTrabalhoObjeto.Concluido
                || item.GetProperty("AusenciaConfirmadaEmUtc").GetDateTimeOffset().Offset != TimeSpan.Zero
                || !(estado == EstadoFoto.Inativa && tipo == TipoTrabalhoObjeto.RemocaoRetida
                    || estado == EstadoFoto.Abandonada && tipo == TipoTrabalhoObjeto.CompensarNuncaAtivo)) Negar();
        }
        // Seleção estável de UMA chave, nunca tenta as outras duas como fallback.
        chave = chaves.Order(StringComparer.Ordinal).First();
        ConferirDisco();
    }

    public static PermissaoLeituraAusente Abrir(string backend) => new(backend);

    public void IniciarLeitura()
    {
        ConferirDisco();
        if (Interlocked.CompareExchange(ref operacao, 1, 0) != 0) Negar();
    }

    public void ConferirTransporte(HttpRequestMessage pedido)
    {
        ConferirDisco();
        var atual = ManifestoRetencao.ConferirDestino(Endpoint, pedido);
        if (pedido.Method != HttpMethod.Get || pedido.Content is not null
            || pedido.Headers.Any(h => h.Key.Equals("Range", StringComparison.OrdinalIgnoreCase)
                || h.Key.StartsWith("If-", StringComparison.OrdinalIgnoreCase)
                || h.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) && h.Value.Single() != Endpoint.Authority)
            || antigas.Contains(atual) || atual != chave
            || Volatile.Read(ref operacao) != 1) Negar();
        // Consome antes do envio, inclusive em timeout/redirect/erro: sem retry.
        if (Interlocked.CompareExchange(ref encaminhamentos, 1, 0) != 0) Negar();
    }

    private byte[] LerPrivado()
    {
        ManifestoRetencao.SemLinks(caminho);
        if (File.GetUnixFileMode(caminho) != ArquivoPrivado
            || File.GetUnixFileMode(Path.GetDirectoryName(caminho)!) != PastaPrivada
            || new FileInfo(caminho).Length is <= 0 or > 1_048_576) Negar();
        return File.ReadAllBytes(caminho);
    }

    internal void ConferirDisco()
    {
        if (!CryptographicOperations.FixedTimeEquals(LerPrivado(), original)) Negar();
    }

    private static void ConferirDuplicadas(JsonElement elemento)
    {
        if (elemento.ValueKind == JsonValueKind.Object)
        {
            ManifestoRetencao.SemPropriedadesDuplicadas(elemento);
            foreach (var p in elemento.EnumerateObject()) ConferirDuplicadas(p.Value);
        }
        if (elemento.ValueKind == JsonValueKind.Array)
            foreach (var item in elemento.EnumerateArray()) ConferirDuplicadas(item);
    }

    private static void Negar() => throw new ArmazenamentoObjetoException();
}
