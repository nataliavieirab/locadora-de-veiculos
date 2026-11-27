using Amazon.Runtime;
using System.Runtime.Versioning;
using Locadora.Application.Modulos.Fotos;

namespace Locadora.Ensaios.FotosR2.Retencao;

[SupportedOSPlatform("linux")]
internal sealed class ObjetosRetencao(IArmazenamentoObjeto real, ManifestoRetencao manifesto) : IArmazenamentoObjeto
{
    public int Remocoes { get; private set; }
    public List<ObjetoFoto> Escritos { get; } = [];

    public async Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        objeto.Conferir(bytes.Span);
        manifesto.RegistrarIntencao(objeto);
        if (!Escritos.Any(o => o.Chave == objeto.Chave))
            Escritos.Add(objeto with { Sha256 = objeto.Sha256.ToArray() });
        await real.GravarAsync(objeto, bytes, token);
    }

    public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken token)
    {
        manifesto.ConferirLeitura(objeto);
        return real.LerAsync(objeto, token);
    }

    public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken token)
    {
        manifesto.ConferirLeitura(objeto);
        return real.ConfirmarAusenciaAsync(objeto, token);
    }

    public Task RemoverAsync(ObjetoFoto objeto, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        manifesto.ConferirDelete(objeto);
        Remocoes++;
        return real.RemoverAsync(objeto, token);
    }
}

[SupportedOSPlatform("linux")]
internal sealed class FabricaHttpRetencao(Action<HttpRequestMessage> conferir) : Amazon.Runtime.HttpClientFactory
{
    public FabricaHttpRetencao(ManifestoRetencao manifesto) : this(manifesto.ConferirTransporte) { }

    public override HttpClient CreateHttpClient(IClientConfig clientConfig) => CriarCliente();

    public HttpClient CriarCliente() => CriarClienteSintetico(new HttpClientHandler
    {
        AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
    });

    // O terminal fornecido pela qualificação é puramente em memória, sem sockets.
    public HttpClient CriarClienteSintetico(HttpMessageHandler terminal) =>
        new(new Transporte(conferir) { InnerHandler = terminal }) { Timeout = TimeSpan.FromSeconds(30) };

    private sealed class Transporte(Action<HttpRequestMessage> conferir) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            conferir(pedido);
            if (pedido.Method == HttpMethod.Put)
            {
                pedido.Headers.Remove("If-None-Match");
                pedido.Headers.TryAddWithoutValidation("If-None-Match", "*");
            }
            var resposta = await base.SendAsync(pedido, token);
            if ((int)resposta.StatusCode is >= 300 and <= 399)
            {
                resposta.Dispose();
                throw new ArmazenamentoObjetoException();
            }
            return resposta;
        }
    }
}
