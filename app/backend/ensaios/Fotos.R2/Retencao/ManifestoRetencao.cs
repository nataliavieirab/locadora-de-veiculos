using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;

namespace Locadora.Ensaios.FotosR2.Retencao;

// Atestados SQL são fornecidos pelo chamador; esta classe não consulta nem altera o banco.
[SupportedOSPlatform("linux")]
internal sealed class ManifestoRetencao
{
    internal const string Bucket = "locadora-veiculos-dev";
    internal static readonly string[] PastasAntigas =
    [
        "r2-gate-309d5e2638bc4526b1b1c563f903f46a",
        "r2-gate-6f77928d13e94b08a09f7dbd6af3bbff",
    ];
    private const UnixFileMode PrivadoArquivo = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivadoPasta = PrivadoArquivo | UnixFileMode.UserExecute;
    private readonly object trava = new();
    private readonly HashSet<string> antigas = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> empresasAntigas = [];
    private readonly HashSet<Guid> empresas = [];
    private readonly HashSet<string> catalogosAntigos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Registro> registros = new(StringComparer.Ordinal);
    private readonly List<string> verificacoes = [];
    private readonly Uri endpoint;
    private readonly string catalogo;
    private readonly TimeProvider tempo;
    private byte[] ultimaPersistencia = [];
    private bool falhou;
    internal bool FalharPublicacaoParaQualificacao { private get; set; }
    internal bool FalharSincronizacaoParaQualificacao { private get; set; }
    public string Caminho { get; }

    private sealed record Registro(string Chave, string Mime, int TamanhoBytes, string Sha256,
        Guid Empresa, EstadoFoto Estado = EstadoFoto.Pendente, Guid? FotoId = null,
        Guid? TrabalhoId = null, TipoTrabalhoObjeto? Tipo = null, EstadoTrabalhoObjeto? EstadoTrabalho = null,
        DateTimeOffset? RemovidaEmUtc = null, DateTimeOffset? ExcluirAposUtc = null,
        DateTimeOffset? EnvioConfirmadoEmUtc = null, bool SemReferenciasProtegidas = false,
        DateTimeOffset? AusenciaConfirmadaEmUtc = null);

    private ManifestoRetencao(string backend, string catalogo, Uri endpoint, TimeProvider tempo)
    {
        if (!OperatingSystem.IsLinux()) Negar();
        this.endpoint = endpoint;
        this.catalogo = catalogo;
        this.tempo = tempo;
        new ConfiguracaoR2
        {
            Endpoint = endpoint.AbsoluteUri, Bucket = Bucket, BucketPrivadoConfirmado = true,
            AccessKey = "validacao-offline", SecretKey = "validacao-offline",
        }.Validar();
        if (endpoint.OriginalString.TrimEnd('/') != endpoint.GetLeftPart(UriPartial.Authority)) Negar();
        if (!CatalogoValido(catalogo)) Negar();
        backend = Path.GetFullPath(backend);
        SemLinks(backend);
        for (var i = 0; i < PastasAntigas.Length; i++)
        {
            var caminho = Path.Combine(backend, "obj", PastasAntigas[i], "evidencias.json");
            SemLinks(caminho);
            if (new FileInfo(caminho).Length is <= 0 or > 1_048_576) Negar();
            using var json = JsonDocument.Parse(File.ReadAllBytes(caminho));
            var raiz = json.RootElement;
            SemPropriedadesDuplicadas(raiz);
            var antigo = raiz.GetProperty("catalogo").GetString()!;
            if (raiz.GetProperty("bucket").GetString() != Bucket || !CatalogoValido(antigo)
                || !catalogosAntigos.Add(antigo) || string.Equals(antigo, catalogo, StringComparison.OrdinalIgnoreCase))
                Negar();
            var objetos = raiz.GetProperty("objetos");
            if (objetos.ValueKind != JsonValueKind.Array || objetos.GetArrayLength() != (i == 0 ? 1 : 4)) Negar();
            foreach (var item in objetos.EnumerateArray())
            {
                SemPropriedadesDuplicadas(item);
                var objeto = new ObjetoFoto(item.GetProperty("Chave").GetString()!,
                    item.GetProperty("Mime").GetString()!, item.GetProperty("TamanhoBytes").GetInt32(),
                    Convert.FromHexString(item.GetProperty("sha256").GetString()!));
                objeto.Validar();
                ChaveCanonica(objeto.Chave);
                if (!antigas.Add(objeto.Chave)) Negar();
                empresasAntigas.Add(Guid.ParseExact(objeto.Chave.Split('/')[0], "N"));
            }
        }
        if (antigas.Count != 5) Negar();
        var pasta = Path.Combine(backend, "obj", "r2-retencao-" + Guid.NewGuid().ToString("N"));
        if (Path.Exists(pasta)) Negar();
        Directory.CreateDirectory(pasta, PrivadoPasta);
        SincronizarDiretorio(Path.GetDirectoryName(pasta)!);
        Caminho = Path.Combine(pasta, "evidencias.json");
        Persistir(registros, empresas);
    }

    public static ManifestoRetencao Criar(string backend, string catalogo, Uri endpoint, TimeProvider tempo) =>
        new(backend, catalogo, endpoint, tempo);

    public void AutorizarEmpresa(Guid empresa)
    {
        lock (trava)
        {
            ConferirDisco();
            if (empresa == Guid.Empty || empresasAntigas.Contains(empresa)) Negar();
            var novas = new HashSet<Guid>(empresas) { empresa };
            Persistir(registros, novas);
            empresas.Add(empresa);
        }
    }

    public void RegistrarIntencao(ObjetoFoto objeto)
    {
        lock (trava)
        {
            ConferirDisco();
            objeto.Validar();
            ChaveCanonica(objeto.Chave);
            var empresa = Guid.ParseExact(objeto.Chave.Split('/')[0], "N");
            if (antigas.Contains(objeto.Chave) || !empresas.Contains(empresa)) Negar();
            // Repetição idêntica é apenas o ensaio de precondição 412; o transporte impõe '*'.
            if (registros.ContainsKey(objeto.Chave)) { Conhecido(objeto); return; }
            if (registros.Count >= 6) Negar();
            var registro = new Registro(objeto.Chave, objeto.Mime, objeto.TamanhoBytes,
                Convert.ToHexString(objeto.Sha256), empresa);
            var novos = new Dictionary<string, Registro>(registros, StringComparer.Ordinal) { [objeto.Chave] = registro };
            Persistir(novos, empresas);
            registros.Add(objeto.Chave, registro);
        }
    }

    public void Atualizar(FotoObjeto foto, TrabalhoObjeto trabalho, bool semReferenciasProtegidas)
    {
        lock (trava)
        {
            ConferirDisco();
            var anterior = Conhecido(new(foto.Chave, foto.Mime, foto.TamanhoBytes, foto.Sha256));
            if (foto.Provedor != ProvedorFoto.R2 || foto.Bucket != Bucket || foto.RetencaoLegada
                || foto.EmpresaId != anterior.Empresa || trabalho.EmpresaId != foto.EmpresaId
                || trabalho.FotoObjetoId != foto.Id || foto.Id.ToString("N") != foto.Chave.Split('/')[1]
                || !Enum.IsDefined(foto.Estado) || !Enum.IsDefined(trabalho.Tipo)
                || !Enum.IsDefined(trabalho.Estado)
                || (anterior.FotoId is { } id && id != foto.Id)
                || (anterior.TrabalhoId is { } tid && tid != trabalho.Id)
                || Mudou(anterior.RemovidaEmUtc, foto.RemovidaEmUtc)
                || Mudou(anterior.ExcluirAposUtc, foto.ExcluirAposUtc)
                || Mudou(anterior.EnvioConfirmadoEmUtc, trabalho.EnvioConfirmadoEmUtc)
                || (anterior.Estado == EstadoFoto.Inativa && foto.Estado != EstadoFoto.Inativa)
                || (anterior.Estado == EstadoFoto.Abandonada && foto.Estado != EstadoFoto.Abandonada)
                || (anterior.Estado == EstadoFoto.Ativa && foto.Estado is EstadoFoto.Pendente or EstadoFoto.Abandonada))
                Negar();
            if (foto.Estado == EstadoFoto.Inativa
                && (foto.RemovidaEmUtc is not { } remocao || foto.ExcluirAposUtc is not { } piso
                    || piso < remocao.AddDays(30) || trabalho.Tipo != TipoTrabalhoObjeto.RemocaoRetida))
                Negar();
            if (foto.Estado == EstadoFoto.Abandonada
                && (foto.RemovidaEmUtc is not null || foto.ExcluirAposUtc is not null
                    || trabalho.Tipo != TipoTrabalhoObjeto.CompensarNuncaAtivo)) Negar();
            var atualizado = anterior with
            {
                FotoId = foto.Id, TrabalhoId = trabalho.Id, Estado = foto.Estado,
                Tipo = trabalho.Tipo, EstadoTrabalho = trabalho.Estado,
                RemovidaEmUtc = foto.RemovidaEmUtc, ExcluirAposUtc = foto.ExcluirAposUtc,
                EnvioConfirmadoEmUtc = trabalho.EnvioConfirmadoEmUtc,
                SemReferenciasProtegidas = semReferenciasProtegidas,
            };
            var novos = new Dictionary<string, Registro>(registros, StringComparer.Ordinal) { [foto.Chave] = atualizado };
            Persistir(novos, empresas);
            registros[foto.Chave] = atualizado;
        }
    }

    public void ConferirLeitura(ObjetoFoto objeto)
    {
        lock (trava) { ConferirDisco(); Conhecido(objeto); }
    }

    public void RegistrarVerificacao(string codigo)
    {
        lock (trava)
        {
            ConferirDisco();
            // Somente código técnico, nunca mensagem de exceção, URL ou chave.
            if (string.IsNullOrEmpty(codigo) || codigo.Length > 96
                || codigo[0] is < 'a' or > 'z' || codigo[^1] == '-' || codigo.Contains("--", StringComparison.Ordinal)
                || codigo.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))) Negar();
            if (verificacoes.Contains(codigo, StringComparer.Ordinal)) return;
            var novas = verificacoes.Append(codigo).ToArray();
            Persistir(registros, empresas, novas);
            verificacoes.Add(codigo);
        }
    }

    // O chamador só registra depois de HEAD negativo observado; não infere ausência do DELETE.
    public void RegistrarAusencia(ObjetoFoto objeto)
    {
        lock (trava)
        {
            ConferirDisco();
            var registro = Conhecido(objeto);
            Elegivel(registro);
            if (registro.AusenciaConfirmadaEmUtc is not null) return;
            var novos = new Dictionary<string, Registro>(registros, StringComparer.Ordinal)
            {
                [objeto.Chave] = registro with { AusenciaConfirmadaEmUtc = tempo.GetUtcNow() },
            };
            Persistir(novos, empresas);
            registros[objeto.Chave] = novos[objeto.Chave];
        }
    }

    public void ConferirDelete(ObjetoFoto objeto)
    {
        lock (trava) { ConferirDisco(); Elegivel(Conhecido(objeto)); }
    }

    public void ConferirTransporte(HttpRequestMessage pedido)
    {
        lock (trava)
        {
            ConferirDisco();
            var chave = ConferirDestino(endpoint, pedido);
            if (antigas.Contains(chave) || !registros.TryGetValue(chave, out var registro)) Negar();
            if (pedido.Method == HttpMethod.Delete) Elegivel(registros[chave]);
        }
    }

    // Compartilhada pelos modos; o modo GET único acrescenta sua restrição de método/cota.
    internal static string ConferirDestino(Uri endpoint, HttpRequestMessage pedido)
    {
        var uri = pedido.RequestUri;
        var operacao = pedido.Method.Method switch
        {
            "PUT" => "PutObject", "GET" => "GetObject", "HEAD" => "HeadObject",
            "DELETE" => "DeleteObject", _ => "",
        };
        var prefixo = "/" + Bucket + "/";
        if (uri is null || !uri.IsAbsoluteUri || operacao == "" || uri.Scheme != "https"
            || uri.Host != endpoint.Host || !uri.IsDefaultPort || uri.Authority != endpoint.Authority
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0
            || uri.OriginalString.Contains('%') || uri.OriginalString.Contains('\\')
            || !uri.AbsolutePath.StartsWith(prefixo, StringComparison.Ordinal)
            || uri.OriginalString != uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath + uri.Query
            || (uri.Query != "" && uri.Query != "?x-id=" + operacao)) Negar();
        if (pedido.Headers.Any(h => h.Key.StartsWith("x-amz-copy-source", StringComparison.OrdinalIgnoreCase)
            || h.Key.Equals("x-amz-acl", StringComparison.OrdinalIgnoreCase)
            || h.Key.StartsWith("x-amz-grant-", StringComparison.OrdinalIgnoreCase)
            || h.Key.Equals("x-amz-website-redirect-location", StringComparison.OrdinalIgnoreCase)))
            Negar();
        var chave = uri!.AbsolutePath[prefixo.Length..];
        ChaveCanonica(chave);
        return chave;
    }

    private Registro Conhecido(ObjetoFoto objeto)
    {
        objeto.Validar();
        ChaveCanonica(objeto.Chave);
        if (antigas.Contains(objeto.Chave) || !registros.TryGetValue(objeto.Chave, out var r)
            || !empresas.Contains(r.Empresa) || r.Mime != objeto.Mime || r.TamanhoBytes != objeto.TamanhoBytes
            || r.Sha256 != Convert.ToHexString(objeto.Sha256)) Negar();
        return registros[objeto.Chave];
    }

    private void Elegivel(Registro r)
    {
        var agora = tempo.GetUtcNow();
        if (!r.SemReferenciasProtegidas || r.FotoId is null || r.TrabalhoId is null
            || !(r.Estado == EstadoFoto.Inativa && r.Tipo == TipoTrabalhoObjeto.RemocaoRetida
                && r.RemovidaEmUtc is { } remocao && r.ExcluirAposUtc is { } piso
                && agora >= piso && agora >= remocao.AddDays(30)
                || r.Estado == EstadoFoto.Abandonada && r.Tipo == TipoTrabalhoObjeto.CompensarNuncaAtivo
                && r.EnvioConfirmadoEmUtc is { } ack && ack <= agora)) Negar();
    }

    private void Persistir(Dictionary<string, Registro> novos, HashSet<Guid> autorizadas,
        IReadOnlyCollection<string>? novasVerificacoes = null)
    {
        var temporario = Path.Combine(Path.GetDirectoryName(Caminho)!, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            SemLinks(Path.GetDirectoryName(Caminho)!);
            if (ultimaPersistencia.Length != 0) ConferirDisco();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                bucket = Bucket, catalogo, endpoint = endpoint.AbsoluteUri,
                empresas = autorizadas.Order().ToArray(), denylist = antigas.Order(StringComparer.Ordinal).ToArray(),
                catalogosProtegidos = catalogosAntigos.Order(StringComparer.Ordinal).ToArray(),
                verificacoes = novasVerificacoes ?? verificacoes,
                objetos = novos.Values.OrderBy(r => r.Chave, StringComparer.Ordinal).ToArray(),
            });
            using (var arquivo = new FileStream(temporario, new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                UnixCreateMode = PrivadoArquivo, Options = FileOptions.WriteThrough,
            }))
            {
                arquivo.Write(bytes);
                arquivo.Flush(flushToDisk: true);
            }
            if (FalharPublicacaoParaQualificacao)
                throw new IOException("Falha sintética de publicação durável.");
            File.Move(temporario, Caminho, overwrite: ultimaPersistencia.Length != 0);
            if (FalharSincronizacaoParaQualificacao)
                throw new IOException("Falha sintética de sincronização do diretório.");
            SincronizarDiretorio(Path.GetDirectoryName(Caminho)!);
            ultimaPersistencia = bytes;
            ConferirDisco();
        }
        catch
        {
            // Uma falha de durabilidade invalida toda a allowlist desta instância.
            falhou = true;
            throw;
        }
    }

    private void ConferirDisco()
    {
        if (falhou) Negar();
        try
        {
            SemLinks(Caminho);
            if (new FileInfo(Caminho).Length != ultimaPersistencia.Length
                || File.GetUnixFileMode(Caminho) != PrivadoArquivo
                || File.GetUnixFileMode(Path.GetDirectoryName(Caminho)!) != PrivadoPasta
                || !CryptographicOperations.FixedTimeEquals(File.ReadAllBytes(Caminho), ultimaPersistencia)) Negar();
        }
        catch { falhou = true; throw; }
    }

    internal static void SemLinks(string caminho)
    {
        for (var atual = Path.GetFullPath(caminho); ;)
        {
            if ((File.GetAttributes(atual) & FileAttributes.ReparsePoint) != 0) Negar();
            var pai = Path.GetDirectoryName(atual);
            if (pai is null || pai == atual) break;
            atual = pai;
        }
    }

    internal static bool CatalogoValido(string? valor) => valor is { Length: 41 }
        && valor.StartsWith("locadora_", StringComparison.Ordinal)
        && Guid.TryParseExact(valor[9..], "N", out var id) && id != Guid.Empty
        && valor[9..] == id.ToString("N");
    internal static void ChaveCanonica(string chave)
    {
        var partes = chave.Split('/');
        if (partes.Length != 2 || partes.Any(p => !Guid.TryParseExact(p, "N", out var id)
            || id == Guid.Empty || p != id.ToString("N"))) Negar();
    }
    private static bool Mudou(DateTimeOffset? antes, DateTimeOffset? depois) => antes is not null && antes != depois;
    // Flush do arquivo não torna o rename durável. Linux exige fsync do diretório;
    // qualquer erro impede liberar a allowlist/PUT. Não segue link no alvo.
    private static void SincronizarDiretorio(string caminho)
    {
        const int oDirectory = 0x10000, oNoFollow = 0x20000, oCloExec = 0x80000;
        var descritor = open(caminho, oDirectory | oNoFollow | oCloExec);
        if (descritor < 0) throw new IOException("Não foi possível sincronizar o manifesto privado.");
        try
        {
            if (fsync(descritor) != 0) throw new IOException("Não foi possível sincronizar o manifesto privado.");
        }
        finally { close(descritor); }
    }
    [DllImport("libc", SetLastError = true)] private static extern int open(string caminho, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int fsync(int descritor);
    [DllImport("libc", SetLastError = true)] private static extern int close(int descritor);
    internal static void SemPropriedadesDuplicadas(JsonElement objeto)
    {
        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var propriedade in objeto.EnumerateObject()) if (!nomes.Add(propriedade.Name)) Negar();
    }
    [DoesNotReturn]
    private static void Negar() => throw new ArmazenamentoObjetoException();
}
