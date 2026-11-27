using System.Net;
using System.Runtime.Versioning;
using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using static Locadora.IntegrationTests.Modulos.Fotos.FotosHttpTests;

namespace Locadora.IntegrationTests.Modulos.Fotos;

// HTTP/TestServer + SQL Server + volume real. Falhas controladas não são outage R2.
[TestClass, SupportedOSPlatform("linux")]
public sealed class GateRecuperacaoFotosHttpTests
{
    [TestMethod]
    public async Task Leitura503RecuperaAposReaberturaSemAlterarBytesReferenciaOuVersao()
    {
        using var e = new Ensaio();
        using var cliente = await ClienteAsync(e.Fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        using var envio = await EnviarAsync(cliente, id, etag);
        Assert.AreEqual(HttpStatusCode.OK, envio.StatusCode);
        var objeto = e.Objetos.Gravados.Single();
        var (referencia, versao) = await DetalheAsync(cliente, id);
        e.Objetos.Ocultar(objeto);
        try
        {
            using var falha = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
            await ConferirErroAsync(falha, e, objeto);
            Assert.AreEqual((referencia, versao), await DetalheAsync(cliente, id));
            await using var banco = Banco(objeto);
            Assert.AreEqual(EstadoFoto.Ativa, (await banco.FotosObjetos.SingleAsync()).Estado);
            Assert.AreEqual(EstadoTrabalhoObjeto.Retido, (await banco.TrabalhosObjetos.SingleAsync()).Estado);
            Assert.AreEqual(0, e.Objetos.Remocoes);
        }
        finally { e.Objetos.Restaurar(objeto); }

        // Novos descritores, host e contexto; não dependem do dicionário de um mock.
        e.Reabrir();
        using var recuperado = ClienteReaberto(e, cliente);
        using var leitura = await recuperado.GetAsync($"/api/v1/veiculos/{id}/foto");
        ConferirHeaders(leitura);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await leitura.Content.ReadAsByteArrayAsync());
        Assert.AreEqual((referencia, versao), await DetalheAsync(recuperado, id));
        await using var posterior = Banco(objeto);
        Assert.AreEqual(1, await posterior.FotosObjetos.CountAsync());
        Assert.AreEqual(1, await posterior.TrabalhosObjetos.CountAsync());
    }

    [TestMethod]
    public async Task FalhaDeConfirmacaoAposPutMantemAtivaEStartupCompensaSomenteNuncaAtiva()
    {
        using var e = new Ensaio();
        using var cliente = await ClienteAsync(e.Fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        using var envio = await EnviarAsync(cliente, id, etag);
        Assert.AreEqual(HttpStatusCode.OK, envio.StatusCode);
        var (referencia, versao) = await DetalheAsync(cliente, id);
        var ativa = e.Objetos.Gravados.Single();
        e.Objetos.OcultarProximaGravacao = true;
        using var troca = await EnviarAsync(cliente, id, versao);
        var pendente = e.Objetos.Gravados.Last();
        try
        {
            await ConferirErroAsync(troca, e, pendente);
            Assert.AreEqual((referencia, versao), await DetalheAsync(cliente, id));
            using var leitura = await cliente.GetAsync($"/api/v1/veiculos/{id}/foto");
            ConferirHeaders(leitura);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await leitura.Content.ReadAsByteArrayAsync());
            await using var banco = Banco(ativa);
            var trabalho = await banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId != Guid.ParseExact(ativa.Chave.Split('/')[1], "N"));
            Assert.IsTrue(trabalho.EnvioConfirmado);
            Assert.AreEqual(EstadoTrabalhoObjeto.EmExecucao, trabalho.Estado);
            Assert.AreEqual(0, e.Objetos.Remocoes, "Não compensar no catch nem apagar a referência anterior.");
        }
        finally { e.Objetos.Restaurar(pendente); }

        e.Tempo.Agora = e.Tempo.Agora.AddMinutes(3);
        e.Reabrir();
        using var recuperado = ClienteReaberto(e, cliente); // startup real executa passagem finita
        await ConferirCompensacaoAsync(e, ativa, pendente);
        Assert.AreEqual((referencia, versao), await DetalheAsync(recuperado, id));
        using var atual = await recuperado.GetAsync($"/api/v1/veiculos/{id}/foto");
        ConferirHeaders(atual);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await atual.Content.ReadAsByteArrayAsync());
        e.Reabrir();
        using var repetido = ClienteReaberto(e, cliente);
        Assert.AreEqual(0, e.Objetos.Remocoes, "Startup repetido não deve excluir de novo trabalho concluído.");
    }

    [TestMethod]
    public async Task DeleteComRespostaPerdidaPermanecePendenteEAusenciaSoConcluiNaNovaPassagem()
    {
        using var e = new Ensaio();
        using var cliente = await ClienteAsync(e.Fabrica);
        var (id, etag) = await VeiculoAsync(cliente);
        using var envio = await EnviarAsync(cliente, id, etag);
        var ativa = e.Objetos.Gravados.Single();
        var (_, versao) = await DetalheAsync(cliente, id);
        e.Objetos.OcultarProximaGravacao = true;
        using var troca = await EnviarAsync(cliente, id, versao);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, troca.StatusCode);
        var pendente = e.Objetos.Gravados.Last();
        e.Objetos.Restaurar(pendente);
        e.Tempo.Agora = e.Tempo.Agora.AddMinutes(3);
        e.Reabrir(perderDelete: true);
        using var primeiro = ClienteReaberto(e, cliente);
        await using (var banco = Banco(ativa))
        {
            var trabalho = await banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == Guid.ParseExact(pendente.Chave.Split('/')[1], "N"));
            Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, trabalho.Estado);
            Assert.AreEqual(1, trabalho.Tentativas);
            Assert.IsTrue(trabalho.EnvioConfirmado);
            Assert.IsNotNull(trabalho.CodigoFalha);
            Assert.IsTrue(await e.Volume.ConfirmarAusenciaAsync(pendente, default));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await e.Volume.LerAsync(ativa, default));
            e.Tempo.Agora = trabalho.ProximoProcessamentoUtc;
        }
        e.Reabrir();
        using var segundo = ClienteReaberto(e, cliente);
        await ConferirCompensacaoAsync(e, ativa, pendente);
        using var leitura = await segundo.GetAsync($"/api/v1/veiculos/{id}/foto");
        ConferirHeaders(leitura);
    }

    private static async Task ConferirCompensacaoAsync(Ensaio e, ObjetoFoto ativa, ObjetoFoto pendente)
    {
        await using var banco = Banco(ativa);
        var idPendente = Guid.ParseExact(pendente.Chave.Split('/')[1], "N");
        var idAtiva = Guid.ParseExact(ativa.Chave.Split('/')[1], "N");
        Assert.AreEqual(2, await banco.FotosObjetos.CountAsync());
        Assert.AreEqual(2, await banco.TrabalhosObjetos.CountAsync());
        var fotoAtiva = await banco.FotosObjetos.SingleAsync(f => f.Id == idAtiva);
        Assert.AreEqual(EstadoFoto.Ativa, fotoAtiva.Estado);
        Assert.IsNull(fotoAtiva.RemovidaEmUtc);
        Assert.IsNull(fotoAtiva.ExcluirAposUtc);
        var trabalhoAtiva = await banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == idAtiva);
        Assert.AreEqual(TipoTrabalhoObjeto.RemocaoRetida, trabalhoAtiva.Tipo);
        Assert.AreEqual(EstadoTrabalhoObjeto.Retido, trabalhoAtiva.Estado);
        Assert.AreEqual(idAtiva, (await banco.Veiculos.SingleAsync()).FotoAtivaId);
        Assert.AreEqual(EstadoFoto.Abandonada, (await banco.FotosObjetos.SingleAsync(f => f.Id == idPendente)).Estado);
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido,
            (await banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == idPendente)).Estado);
        Assert.IsTrue(await e.Volume.ConfirmarAusenciaAsync(pendente, default));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await e.Volume.LerAsync(ativa, default));
        Assert.AreEqual(1, e.Objetos.Remocoes);
    }

    private static LocadoraDbContext Banco(ObjetoFoto objeto) =>
        AmbienteIntegracao.CriarContexto(contexto: new ContextoEmpresa
        { EmpresaId = Guid.ParseExact(objeto.Chave.Split('/')[0], "N") });

    private static HttpClient ClienteReaberto(Ensaio e, HttpClient anterior)
    {
        var novo = e.Fabrica.CreateClient();
        novo.DefaultRequestHeaders.Authorization = anterior.DefaultRequestHeaders.Authorization;
        return novo;
    }

    private static async Task<(string Referencia, string Versao)> DetalheAsync(HttpClient cliente, Guid id)
    {
        using var detalhe = await cliente.GetAsync($"/api/v1/veiculos/{id}");
        Assert.AreEqual(HttpStatusCode.OK, detalhe.StatusCode);
        return ((await ApiCliente.JsonAsync(detalhe)).GetProperty("foto").GetRawText(), detalhe.Headers.ETag!.ToString());
    }

    private static void ConferirHeaders(HttpResponseMessage resposta)
    {
        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        Assert.IsTrue(resposta.Headers.CacheControl!.Private && resposta.Headers.CacheControl.NoStore);
        Assert.AreEqual("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.AreEqual("image/png", resposta.Content.Headers.ContentType!.MediaType);
        Assert.AreEqual(3L, resposta.Content.Headers.ContentLength);
        Assert.IsNull(resposta.Headers.Location);
        Assert.IsNull(resposta.Content.Headers.ContentDisposition);
    }

    private static async Task ConferirErroAsync(HttpResponseMessage resposta, Ensaio e, ObjetoFoto objeto)
    {
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.AreEqual("application/problem+json", resposta.Content.Headers.ContentType!.MediaType);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.IsFalse(texto.Contains(e.Raiz, StringComparison.Ordinal));
        Assert.IsFalse(texto.Contains(objeto.Chave, StringComparison.Ordinal));
        Assert.IsNull(resposta.Headers.Location);
    }

    private sealed class Relogio : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed class Ensaio : IDisposable
    {
        public string Raiz { get; } = Path.GetFullPath(Path.Combine("obj", "gate-http-recuperacao-" + Guid.NewGuid().ToString("N")));
        public Relogio Tempo { get; } = new();
        private readonly DestinoFotos destino = new(ProvedorFoto.Local, "http-" + Guid.NewGuid().ToString("N"));
        public ArmazenamentoLocal Volume { get; private set; } = null!;
        public ObjetosVolume Objetos { get; private set; } = null!;
        public FabricaApi Fabrica { get; private set; } = null!;

        public Ensaio()
        {
            Directory.CreateDirectory(Raiz, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Reabrir();
        }

        public void Reabrir(bool perderDelete = false)
        {
            Fabrica?.Dispose();
            Volume?.Dispose();
            Volume = new ArmazenamentoLocal(Raiz, Raiz + "/webroot");
            Objetos = new ObjetosVolume(Volume, Raiz) { PerderRespostaDelete = perderDelete };
            Fabrica = FotosHttpTests.Fabrica(Objetos, destino: destino, tempo: Tempo, retomar: true);
        }

        public void Dispose()
        {
            Fabrica.Dispose();
            Volume.Dispose();
            // Preservar o volume sintético ignorado; não fazer cleanup de bytes ativos.
        }
    }

    private sealed class ObjetosVolume(ArmazenamentoLocal volume, string raiz) : IArmazenamentoObjeto
    {
        public List<ObjetoFoto> Gravados { get; } = [];
        public bool OcultarProximaGravacao { get; set; }
        public bool PerderRespostaDelete { get; set; }
        public int Remocoes { get; private set; }
        public void Ocultar(ObjetoFoto objeto) => File.Move(Path.Combine(raiz, objeto.Chave), Path.Combine(raiz, objeto.Chave) + ".indisponivel");
        public void Restaurar(ObjetoFoto objeto) => File.Move(Path.Combine(raiz, objeto.Chave) + ".indisponivel", Path.Combine(raiz, objeto.Chave));
        public async Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            await volume.GravarAsync(objeto, bytes, token);
            Gravados.Add(objeto);
            if (OcultarProximaGravacao) { OcultarProximaGravacao = false; Ocultar(objeto); }
        }
        public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken token) => volume.LerAsync(objeto, token);
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken token) => volume.ConfirmarAusenciaAsync(objeto, token);
        public async Task RemoverAsync(ObjetoFoto objeto, CancellationToken token)
        {
            Remocoes++;
            await volume.RemoverAsync(objeto, token);
            if (PerderRespostaDelete) throw new ArmazenamentoObjetoException();
        }
    }
}
