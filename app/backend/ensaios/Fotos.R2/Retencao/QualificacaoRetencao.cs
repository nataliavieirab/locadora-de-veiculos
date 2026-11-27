using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;

namespace Locadora.Ensaios.FotosR2.Retencao;

// Executável somente por opção explícita da entrada. Não instancia SDK, host, SQL ou sockets.
[SupportedOSPlatform("linux")]
internal static class QualificacaoRetencao
{
    private static readonly Uri Endpoint = new("https://00000000000000000000000000000000.r2.cloudflarestorage.com");
    private static readonly byte[] Bytes = [17, 29, 43];
    private const UnixFileMode ArquivoPrivado = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PastaPrivada = ArquivoPrivado | UnixFileMode.UserExecute;

    public static async Task ExecutarAsync(string backend)
    {
        if (!OperatingSystem.IsLinux()) throw new InvalidOperationException();
        // Nenhum caminho dos gates reais é aberto: backend fictício tem sua própria árvore obj.
        var raiz = Path.Combine(Path.GetFullPath(backend), "obj",
            "r2-retencao-qualificacao-" + Guid.NewGuid().ToString("N"));
        ManifestoRetencao.SemLinks(Path.GetFullPath(backend));
        Directory.CreateDirectory(raiz, PastaPrivada);
        var contagem = new Contagem();
        var cenario = new Cenario(raiz);
        var manifesto = cenario.Criar();
        var empresa = Guid.NewGuid();
        manifesto.AutorizarEmpresa(empresa);
        var memoria = new ObjetosMemoria(manifesto);
        var objetos = new ObjetosRetencao(memoria, manifesto);
        var terminal = new Terminal();
        using var http = new FabricaHttpRetencao(manifesto).CriarClienteSintetico(terminal);
        var inicio = cenario.Relogio.Agora;
        var (veiculo, foto) = Preparar(empresa, inicio);
        var objeto = Objeto(foto);
        var trabalho = TrabalhoObjeto.Criar(foto, inicio);
        await objetos.GravarAsync(objeto, Bytes, default);
        Exigir(memoria.Gravacoes == 1 && memoria.ManifestoPrecedeuPut && objetos.Escritos.Count == 1);
        await objetos.GravarAsync(objeto, Bytes, default);
        Exigir(memoria.Gravacoes == 2 && objetos.Escritos.Count == 1);
        await NegarAsync(() => objetos.GravarAsync(objeto with { Mime = "image/jpeg" }, Bytes, default), contagem);
        manifesto.Atualizar(foto, trabalho, true);
        await NegarAsync(() => objetos.RemoverAsync(objeto, default), contagem);
        await NegarPedido(http, HttpMethod.Delete, Destino(objeto), contagem);
        veiculo.DefinirFoto(foto, null, inicio);
        manifesto.Atualizar(foto, trabalho, true);
        cenario.Relogio.Agora = inicio.AddDays(90);
        await NegarAsync(() => objetos.RemoverAsync(objeto, default), contagem);
        await NegarPedido(http, HttpMethod.Delete, Destino(objeto), contagem);

        var remocao = inicio.AddDays(90);
        veiculo.RemoverFoto(foto, remocao);
        // O trabalho nasce como compensação; a retomada pode convertê-lo em remoção retida.
        var posse = trabalho.Adquirir(remocao);
        trabalho.Reter(posse, foto, remocao);
        manifesto.Atualizar(foto, trabalho, true);
        cenario.Relogio.Agora = remocao.AddDays(30).AddTicks(-1);
        await NegarAsync(() => objetos.RemoverAsync(objeto, default), contagem);
        await NegarPedido(http, HttpMethod.Delete, Destino(objeto), contagem);
        cenario.Relogio.Agora = remocao.AddDays(30);
        manifesto.Atualizar(foto, trabalho, false);
        await NegarAsync(() => objetos.RemoverAsync(objeto, default), contagem);
        await NegarPedido(http, HttpMethod.Delete, Destino(objeto), contagem);
        manifesto.Atualizar(foto, trabalho, true);
        await objetos.RemoverAsync(objeto, default);
        await PermitirPedido(http, HttpMethod.Delete, Destino(objeto) + "?x-id=DeleteObject");
        Exigir(await objetos.ConfirmarAusenciaAsync(objeto, default));
        manifesto.RegistrarAusencia(objeto);
        manifesto.RegistrarAusencia(objeto);
        cenario.Relogio.Agora = remocao.AddDays(31);
        await objetos.RemoverAsync(objeto, default);
        await PermitirPedido(http, HttpMethod.Delete, Destino(objeto));

        // Compensação nunca ativa: sem ack bloqueia; com ack durável e sem referências permite.
        var (_, abandonada) = Preparar(empresa, inicio);
        var compensacao = TrabalhoObjeto.Criar(abandonada, inicio);
        var objetoAbandonado = Objeto(abandonada);
        await objetos.GravarAsync(objetoAbandonado, Bytes, default);
        abandonada.Abandonar(inicio);
        manifesto.Atualizar(abandonada, compensacao, true);
        await NegarAsync(() => objetos.RemoverAsync(objetoAbandonado, default), contagem);
        await NegarPedido(http, HttpMethod.Delete, Destino(objetoAbandonado), contagem);
        var token = compensacao.Adquirir(inicio);
        compensacao.ConfirmarEnvio(token, inicio);
        manifesto.Atualizar(abandonada, compensacao, false);
        await NegarAsync(() => objetos.RemoverAsync(objetoAbandonado, default), contagem);
        await NegarPedido(http, HttpMethod.Delete, Destino(objetoAbandonado), contagem);
        manifesto.Atualizar(abandonada, compensacao, true);
        await objetos.RemoverAsync(objetoAbandonado, default);
        await PermitirPedido(http, HttpMethod.Delete, Destino(objetoAbandonado));
        manifesto.RegistrarAusencia(objetoAbandonado);
        await NegarAsync(() => { manifesto.AutorizarEmpresa(cenario.EmpresaAntiga); return Task.CompletedTask; }, contagem);
        foreach (var antigo in cenario.Antigos)
        {
            await NegarAsync(() => objetos.GravarAsync(antigo, Bytes, default), contagem);
            await NegarAsync(() => objetos.LerAsync(antigo, default), contagem);
            await NegarAsync(() => objetos.ConfirmarAusenciaAsync(antigo, default), contagem);
            await NegarAsync(() => objetos.RemoverAsync(antigo, default), contagem);
            foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Head, HttpMethod.Put, HttpMethod.Delete })
                await NegarPedido(http, metodo, Destino(antigo), contagem);
        }

        var desconhecido = NovoObjeto(empresa);
        await NegarAsync(() => objetos.LerAsync(desconhecido, default), contagem);
        await NegarAsync(() => objetos.RemoverAsync(desconhecido, default), contagem);
        await NegarAsync(() => objetos.ConfirmarAusenciaAsync(desconhecido, default), contagem);
        await NegarAsync(() => objetos.GravarAsync(NovoObjeto(Guid.NewGuid()), Bytes, default), contagem);
        foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Head, HttpMethod.Put, HttpMethod.Delete })
            await NegarPedido(http, metodo, Destino(desconhecido), contagem);

        // Exercita diretamente o transporte, sem depender do decorador.
        await PermitirPedido(http, HttpMethod.Get, Destino(objeto) + "?x-id=GetObject");
        await PermitirPedido(http, HttpMethod.Head, Destino(objeto) + "?x-id=HeadObject");
        terminal.Status = HttpStatusCode.PreconditionFailed;
        await PermitirPedido(http, HttpMethod.Put, Destino(objeto) + "?x-id=PutObject",
            HttpStatusCode.PreconditionFailed);
        Exigir(terminal.PutsCondicionais == 1);
        terminal.Status = HttpStatusCode.OK;
        var destino = Destino(objeto);
        foreach (var cabecalho in new[] { "x-amz-copy-source", "x-amz-acl", "x-amz-grant-read", "x-amz-website-redirect-location" })
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Put, destino);
            pedido.Headers.TryAddWithoutValidation(cabecalho, "sintetico");
            await NegarAsync(async () => { using var resposta = await http.SendAsync(pedido); }, contagem);
        }
        var proibidos = new[]
        {
            (HttpMethod.Get, "https://example.invalid/" + ManifestoRetencao.Bucket + "/" + objeto.Chave),
            (HttpMethod.Get, destino.Replace(ManifestoRetencao.Bucket, "outro-bucket", StringComparison.Ordinal)),
            (HttpMethod.Get, Endpoint + ManifestoRetencao.Bucket + "/"),
            (HttpMethod.Get, Endpoint.AbsoluteUri),
            (HttpMethod.Post, destino), (HttpMethod.Patch, destino), (HttpMethod.Options, destino),
            (HttpMethod.Get, destino + "?list-type=2"), (HttpMethod.Get, destino + "?acl"),
            (HttpMethod.Put, destino + "?uploads"), (HttpMethod.Put, destino + "?uploadId=teste&partNumber=1"),
            (HttpMethod.Get, destino + "?x-id=PutObject"), (HttpMethod.Put, destino + "?x-id=DeleteObject"),
            (HttpMethod.Delete, destino + "?x-id=GetObject"), (HttpMethod.Head, destino + "?x-id=GetObject"),
            (HttpMethod.Get, destino + "?x-id=GetObject&acl"), (HttpMethod.Get, destino + "#fragmento"),
            (HttpMethod.Get, destino.Replace("https://", "http://", StringComparison.Ordinal)),
            (HttpMethod.Get, destino.Replace("https://", "https://usuario@", StringComparison.Ordinal)),
            (HttpMethod.Get, destino.Replace(".com/", ".com:444/", StringComparison.Ordinal)),
            (HttpMethod.Get, destino.Replace(objeto.Chave, objeto.Chave.Replace("/", "%2F"), StringComparison.Ordinal)),
            (HttpMethod.Get, destino.Replace(objeto.Chave, "%61" + objeto.Chave[1..], StringComparison.Ordinal)),
            (HttpMethod.Get, destino.Replace(objeto.Chave, "prefixo/../" + objeto.Chave, StringComparison.Ordinal)),
            (HttpMethod.Get, destino + "/"), (HttpMethod.Get, destino + "?X-Amz-Algorithm=teste"),
        };
        foreach (var (metodo, uri) in proibidos) await NegarPedido(http, metodo, uri, contagem);
        terminal.Status = HttpStatusCode.TemporaryRedirect;
        var chamadas = terminal.Chamadas;
        await NegarAsync(() => http.GetAsync(destino), contagem);
        Exigir(terminal.Chamadas == chamadas + 1); // Resposta rejeitada, sem seguir Location.
        terminal.Status = HttpStatusCode.OK;
        while (objetos.Escritos.Count < 6) await objetos.GravarAsync(NovoObjeto(empresa), Bytes, default);
        await NegarAsync(() => objetos.GravarAsync(NovoObjeto(empresa), Bytes, default), contagem);
        await NegarAsync(() => { manifesto.RegistrarVerificacao("erro bruto / segredo"); return Task.CompletedTask; }, contagem);
        manifesto.RegistrarVerificacao("qualificacao-offline-sem-rede");
        using (var json = JsonDocument.Parse(File.ReadAllBytes(manifesto.Caminho)))
        {
            Exigir(json.RootElement.GetProperty("verificacoes").EnumerateArray()
                .Any(v => v.GetString() == "qualificacao-offline-sem-rede"));
            Exigir(json.RootElement.GetProperty("objetos").EnumerateArray()
                .Count(o => o.GetProperty("AusenciaConfirmadaEmUtc").ValueKind == JsonValueKind.String) == 2);
        }
        Exigir(objetos.Remocoes == 3 && memoria.Remocoes == 3);

        await ProvarFalhasDisco(raiz, contagem);
        await ProvarDenylist(raiz, contagem);
        Console.WriteLine("PASS qualificação offline de retenção: decorador e transporte independentes; "
            + $"negações verificadas={contagem.Negadas}; remoções sintéticas permitidas=3; "
            + "objetos antigos sintéticos protegidos=5; rede=0; SQL=0; segredos=0. Não constitui prova remota.");
    }

    private static async Task ProvarFalhasDisco(string raiz, Contagem contagem)
    {
        foreach (var falha in new[] { "ausente", "corrompido", "permissao", "symlink", "persistencia", "diretorio" })
        {
            var c = new Cenario(raiz);
            var m = c.Criar();
            var empresa = Guid.NewGuid();
            m.AutorizarEmpresa(empresa);
            var (veiculo, foto) = Preparar(empresa, c.Relogio.Agora);
            var objeto = Objeto(foto);
            m.RegistrarIntencao(objeto);
            veiculo.DefinirFoto(foto, null, c.Relogio.Agora);
            veiculo.RemoverFoto(foto, c.Relogio.Agora);
            var trabalho = TrabalhoObjeto.Criar(foto, c.Relogio.Agora);
            m.Atualizar(foto, trabalho, true);
            c.Relogio.Agora = c.Relogio.Agora.AddDays(30);
            var memoria = new ObjetosMemoria(m);
            var decorador = new ObjetosRetencao(memoria, m);
            var terminal = new Terminal();
            using var http = new FabricaHttpRetencao(m).CriarClienteSintetico(terminal);
            if (falha == "ausente") File.Move(m.Caminho, m.Caminho + ".preservado");
            if (falha == "corrompido") File.WriteAllText(m.Caminho, "{}");
            if (falha == "permissao") File.SetUnixFileMode(m.Caminho, ArquivoPrivado | UnixFileMode.GroupRead);
            if (falha == "symlink")
            {
                File.Move(m.Caminho, m.Caminho + ".preservado");
                File.CreateSymbolicLink(m.Caminho, m.Caminho + ".preservado");
            }
            if (falha == "persistencia")
            {
                // Falha após flush do temporário e antes de Move: o novo registro não pode ser liberado.
                m.FalharPublicacaoParaQualificacao = true;
            }
            if (falha == "diretorio") m.FalharSincronizacaoParaQualificacao = true;
            await NegarAsync(() => decorador.GravarAsync(NovoObjeto(empresa), Bytes, default), contagem);
            await NegarAsync(() => decorador.GravarAsync(objeto, Bytes, default), contagem);
            await NegarAsync(() => decorador.RemoverAsync(objeto, default), contagem);
            await NegarPedido(http, HttpMethod.Put, Destino(objeto), contagem);
            await NegarPedido(http, HttpMethod.Delete, Destino(objeto), contagem);
            Exigir(memoria.Gravacoes == 0 && memoria.Remocoes == 0 && terminal.Chamadas == 0);
        }
    }

    private static async Task ProvarDenylist(string raiz, Contagem contagem)
    {
        foreach (var falha in new[] { "incompleta", "duplicada", "intersecao", "bucket", "catalogo", "ausente", "symlink" })
        {
            var c = new Cenario(raiz);
            if (falha == "incompleta") c.SalvarAntigo(1, c.Antigos.Skip(1).Take(3));
            if (falha == "duplicada") c.SalvarAntigo(1, Enumerable.Repeat(c.Antigos[1], 4));
            if (falha == "intersecao") c.SalvarAntigo(1, c.Antigos.Take(4));
            if (falha == "bucket") c.SalvarAntigo(1, c.Antigos.Skip(1), bucket: "outro-bucket");
            if (falha == "catalogo") c.SalvarAntigo(1, c.Antigos.Skip(1), catalogo: c.Catalogos[0]);
            if (falha == "ausente") File.Move(c.Caminhos[0], c.Caminhos[0] + ".preservado");
            if (falha == "symlink")
            {
                File.Move(c.Caminhos[0], c.Caminhos[0] + ".preservado");
                File.CreateSymbolicLink(c.Caminhos[0], c.Caminhos[0] + ".preservado");
            }
            await NegarAsync(() => { c.Criar(); return Task.CompletedTask; }, contagem);
        }
        var colisao = new Cenario(raiz);
        await NegarAsync(() => { colisao.Criar(colisao.Catalogos[0]); return Task.CompletedTask; }, contagem);
        await NegarAsync(() => { colisao.Criar("locadora_nao-guid"); return Task.CompletedTask; }, contagem);
        await NegarAsync(() =>
        {
            ManifestoRetencao.Criar(colisao.Backend, "locadora_" + Guid.NewGuid().ToString("N"),
                new Uri("https://example.invalid"), colisao.Relogio);
            return Task.CompletedTask;
        }, contagem);
    }

    private static async Task NegarPedido(HttpClient cliente, HttpMethod metodo, string uri, Contagem contagem)
    {
        using var pedido = new HttpRequestMessage(metodo, uri);
        await NegarAsync(async () => { using var resposta = await cliente.SendAsync(pedido); }, contagem);
    }

    private static async Task PermitirPedido(HttpClient cliente, HttpMethod metodo, string uri,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        using var pedido = new HttpRequestMessage(metodo, uri);
        // Até uma precondição fornecida incorretamente é substituída pelo transporte.
        if (metodo == HttpMethod.Put) pedido.Headers.TryAddWithoutValidation("If-None-Match", "incorreto");
        using var resposta = await cliente.SendAsync(pedido);
        Exigir(resposta.StatusCode == status);
    }

    private static async Task NegarAsync(Func<Task> acao, Contagem contagem)
    {
        try { await acao(); }
        catch (Exception erro) when (erro is ArmazenamentoObjetoException or IOException
            or UnauthorizedAccessException or JsonException or FormatException)
        {
            contagem.Negadas++;
            return;
        }
        throw new InvalidOperationException("A proteção offline não negou uma operação sintética.");
    }

    private static (Veiculo Veiculo, FotoObjeto Foto) Preparar(Guid empresa, DateTimeOffset agora)
    {
        var veiculo = Veiculo.Novo(empresa, new("FIC1A01", "Sintética", "Azul", "Ensaio",
            TipoCombustivel.Gasolina, 50, 2026, Guid.NewGuid()), agora);
        var foto = FotoObjeto.Preparar(veiculo, "image/png", Bytes.Length, 1, 1, SHA256.HashData(Bytes),
            new byte[8], ProvedorFoto.R2, ManifestoRetencao.Bucket, Guid.NewGuid(), "offline", agora);
        return (veiculo, foto);
    }

    private static ObjetoFoto Objeto(FotoObjeto foto) => new(foto.Chave, foto.Mime, foto.TamanhoBytes, foto.Sha256);
    private static ObjetoFoto NovoObjeto(Guid empresa) =>
        new($"{empresa:N}/{Guid.NewGuid():N}", "image/png", Bytes.Length, SHA256.HashData(Bytes));
    private static string Destino(ObjetoFoto objeto) => Endpoint + ManifestoRetencao.Bucket + "/" + objeto.Chave;
    private static void Exigir(bool condicao)
    {
        if (!condicao) throw new InvalidOperationException("Verificação offline sintética falhou.");
    }

    private sealed class Contagem { public int Negadas { get; set; } }
    private sealed class Relogio : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed class Cenario
    {
        public string Backend { get; }
        public Relogio Relogio { get; } = new();
        public Guid EmpresaAntiga { get; } = Guid.NewGuid();
        public List<ObjetoFoto> Antigos { get; } = [];
        public string[] Catalogos { get; } = ["locadora_" + Guid.NewGuid().ToString("N"), "locadora_" + Guid.NewGuid().ToString("N")];
        public string[] Caminhos { get; }

        public Cenario(string raiz)
        {
            Backend = Path.Combine(raiz, "backend-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Backend, PastaPrivada);
            Caminhos = ManifestoRetencao.PastasAntigas.Select(p => Path.Combine(Backend, "obj", p, "evidencias.json")).ToArray();
            for (var i = 0; i < 5; i++) Antigos.Add(NovoObjeto(EmpresaAntiga));
            SalvarAntigo(0, Antigos.Take(1));
            SalvarAntigo(1, Antigos.Skip(1));
        }

        public ManifestoRetencao Criar(string? catalogo = null) =>
            ManifestoRetencao.Criar(Backend, catalogo ?? "locadora_" + Guid.NewGuid().ToString("N"), Endpoint, Relogio);

        public void SalvarAntigo(int indice, IEnumerable<ObjetoFoto> objetos,
            string? bucket = null, string? catalogo = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Caminhos[indice])!, PastaPrivada);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                bucket = bucket ?? ManifestoRetencao.Bucket, catalogo = catalogo ?? Catalogos[indice],
                objetos = objetos.Select(o => new { o.Chave, o.Mime, o.TamanhoBytes, sha256 = Convert.ToHexString(o.Sha256) }),
            });
            using var arquivo = new FileStream(Caminhos[indice], new FileStreamOptions
            {
                Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None, UnixCreateMode = ArquivoPrivado,
            });
            arquivo.Write(bytes);
            arquivo.Flush(true);
        }
    }

    private sealed class ObjetosMemoria(ManifestoRetencao manifesto) : IArmazenamentoObjeto
    {
        private readonly HashSet<string> presentes = new(StringComparer.Ordinal);
        public int Gravacoes { get; private set; }
        public int Remocoes { get; private set; }
        public bool ManifestoPrecedeuPut { get; private set; }
        public Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            // Terminal exige a intenção no disco antes de aceitar a chamada.
            using var json = JsonDocument.Parse(File.ReadAllBytes(manifesto.Caminho));
            Exigir(json.RootElement.GetProperty("objetos").EnumerateArray()
                .Any(o => o.GetProperty("Chave").GetString() == objeto.Chave));
            manifesto.ConferirLeitura(objeto);
            ManifestoPrecedeuPut = true;
            Gravacoes++;
            presentes.Add(objeto.Chave);
            return Task.CompletedTask;
        }
        public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken token) => Task.FromResult(Bytes.ToArray());
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken token) => Task.FromResult(!presentes.Contains(objeto.Chave));
        public Task RemoverAsync(ObjetoFoto objeto, CancellationToken token)
        {
            Remocoes++;
            presentes.Remove(objeto.Chave);
            return Task.CompletedTask;
        }
    }

    private sealed class Terminal : HttpMessageHandler
    {
        public int Chamadas { get; private set; }
        public int PutsCondicionais { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken token)
        {
            Chamadas++;
            if (pedido.Method == HttpMethod.Put)
            {
                Exigir(pedido.Headers.TryGetValues("If-None-Match", out var valores) && valores.SequenceEqual(["*"]));
                PutsCondicionais++;
            }
            var resposta = new HttpResponseMessage(Status);
            if ((int)Status is >= 300 and <= 399) resposta.Headers.Location = new Uri("https://example.invalid/");
            return Task.FromResult(resposta);
        }
    }
}
