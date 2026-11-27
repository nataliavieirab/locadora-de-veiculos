using Amazon.Runtime;
using Locadora.Application.Modulos.Fotos;

namespace Locadora.Ensaios.FotosR2;

// Defesa de transporte independente da retomada e do decorador de objetos.
internal sealed class ProtecaoR2(Uri endpoint, string bucket)
{
    public HashSet<string> Empresas { get; } = [];
    public HashSet<string> Chaves { get; } = [];
    public int Enviadas { get; private set; }
    public int Bloqueadas { get; private set; }

    public void Conferir(HttpRequestMessage pedido)
    {
        var uri = pedido.RequestUri;
        var prefixo = "/" + bucket + "/";
        var permitida = pedido.Method == HttpMethod.Get || pedido.Method == HttpMethod.Put || pedido.Method == HttpMethod.Head;
        if (uri is null || uri.Scheme != "https" || uri.Authority != endpoint.Authority
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || !permitida
            || !uri.AbsolutePath.StartsWith(prefixo, StringComparison.Ordinal)
            || !Chaves.Contains(uri.AbsolutePath[prefixo.Length..])
            || uri.Query is not ("" or "?x-id=PutObject" or "?x-id=GetObject" or "?x-id=HeadObject"))
        {
            Bloqueadas++;
            throw new ArmazenamentoObjetoException();
        }
    }

    public void Registrar(ObjetoFoto objeto)
    {
        objeto.Validar();
        var partes = objeto.Chave.Split('/');
        if (partes.Length != 2 || !Empresas.Contains(partes[0])
            || !Guid.TryParseExact(partes[1], "N", out _) || Chaves.Count >= 6 || !Chaves.Add(objeto.Chave))
            throw new ArmazenamentoObjetoException();
    }

    public void ProvarBloqueios()
    {
        var terminal = new TerminalSintetico();
        using var cliente = new HttpClient(new Transporte(this) { InnerHandler = terminal });
        var propria = $"{Guid.NewGuid():N}/{Guid.NewGuid():N}";
        Chaves.Add(propria);
        var baseUri = new Uri(endpoint, bucket + "/" + propria);
        var proibidas = new[]
        {
            new HttpRequestMessage(HttpMethod.Delete, baseUri),
            new HttpRequestMessage(HttpMethod.Post, baseUri),
            new HttpRequestMessage(HttpMethod.Put, new Uri(endpoint, "outro-bucket/" + propria)),
            new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, bucket + "/" + Guid.NewGuid().ToString("N"))),
            new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, bucket + "/")),
            new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri + "?acl")),
            new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/"),
        };
        foreach (var pedido in proibidas)
        {
            using (pedido)
            {
                var negada = false;
                try { using var resposta = cliente.SendAsync(pedido).GetAwaiter().GetResult(); }
                catch (ArmazenamentoObjetoException) { negada = true; }
                if (!negada) throw new InvalidOperationException();
            }
        }
        if (Enviadas != 0 || Bloqueadas != proibidas.Length || terminal.Chamadas != 0) throw new InvalidOperationException();
        using var permitido = cliente.GetAsync(baseUri).GetAwaiter().GetResult();
        if (Enviadas != 1 || terminal.Chamadas != 1) throw new InvalidOperationException();
        Chaves.Remove(propria);
        // O único encaminhamento acima terminou em memória, sem rede.
        Enviadas = 0;
    }

    public HttpClient CriarCliente() => new(new Transporte(this)
    {
        InnerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false },
    }) { Timeout = TimeSpan.FromSeconds(30) };

    private sealed class Transporte(ProtecaoR2 protecao) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken token)
        {
            protecao.Conferir(pedido);
            if (pedido.Method == HttpMethod.Put) pedido.Headers.TryAddWithoutValidation("If-None-Match", "*");
            protecao.Enviadas++;
            return base.SendAsync(pedido, token);
        }
    }

    private sealed class TerminalSintetico : HttpMessageHandler
    {
        public int Chamadas { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken token)
        {
            Chamadas++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}

internal sealed class FabricaHttpProtegida(ProtecaoR2 protecao) : Amazon.Runtime.HttpClientFactory
{
    public override HttpClient CreateHttpClient(IClientConfig clientConfig) => protecao.CriarCliente();
}

internal sealed class ObjetosProtegidos(IArmazenamentoObjeto real, ProtecaoR2 protecao) : IArmazenamentoObjeto
{
    public List<ObjetoFoto> Escritos { get; } = [];
    public bool FalharLeitura { get; set; }
    public int DeletesBloqueados { get; private set; }
    public Action? RegistrarEvidencia { get; set; }

    public async Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        protecao.Registrar(objeto);
        Escritos.Add(objeto);
        // Registrar intenção antes de I/O, inclusive se o PUT ficar incerto.
        RegistrarEvidencia?.Invoke();
        await real.GravarAsync(objeto, bytes, token);
    }

    public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken token)
    {
        if (!protecao.Chaves.Contains(objeto.Chave) || FalharLeitura) throw new ArmazenamentoObjetoException();
        return real.LerAsync(objeto, token);
    }

    public Task RemoverAsync(ObjetoFoto objeto, CancellationToken token)
    {
        DeletesBloqueados++;
        throw new ArmazenamentoObjetoException();
    }

    public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken token)
    {
        if (!protecao.Chaves.Contains(objeto.Chave)) throw new ArmazenamentoObjetoException();
        return real.ConfirmarAusenciaAsync(objeto, token);
    }
}
