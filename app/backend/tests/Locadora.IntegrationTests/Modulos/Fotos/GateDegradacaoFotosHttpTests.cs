using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Locadora.Application.Modulos.Fotos;
using Locadora.Application.Modulos.Transversal;
using Locadora.IntegrationTests.Modulos.Locacoes;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Locadora.IntegrationTests.Modulos.Locacoes.DevolucaoHttpTests;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
public sealed class GateDegradacaoFotosHttpTests
{
    [TestMethod]
    public async Task StorageQueFalhaNaoImpedeCadastroContratacaoDevolucaoOuHistorico()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var objetos = new Indisponiveis();
        await using var fabrica = new FabricaApi(AmbienteIntegracao.Conexao, AmbienteIntegracao.Chave,
            extras: new() { ["Fotos:Provedor"] = "Local", ["Fotos:RaizPrivada"] = "/volume-indisponivel-gate-f5" },
            servicos: s =>
            {
                s.RemoveAll<TimeProvider>();
                s.AddSingleton<TimeProvider>(new Relogio());
                s.RemoveAll<IArmazenamentoObjeto>();
                s.AddSingleton<IArmazenamentoObjeto>(objetos);
                s.RemoveAll<IProcessadorImagem>();
                s.AddSingleton<IProcessadorImagem, Processador>();
                s.RemoveAll<IRetomadaFotos>();
                s.AddSingleton<IRetomadaFotos, SemRetomada>();
            });
        using var cliente = await EstimativasSelecoesTests.AutenticarAsync(c, fabrica);
        using var pedidoFoto = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/veiculos/{c.Dados.Veiculo.Id}/foto");
        pedidoFoto.Headers.TryAddWithoutValidation("If-Match", VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion));
        var multipart = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent([1]);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(arquivo, "arquivo", "sintetico.png");
        pedidoFoto.Content = multipart;
        using var falha = await cliente.SendAsync(pedidoFoto);
        Assert.AreEqual(503, (int)falha.StatusCode);
        Assert.AreEqual(1, objetos.Chamadas);

        using var cadastro = await cliente.PostAsJsonAsync("/api/v1/veiculos", new
        {
            placa = "FIC5A02", marca = "Marca sintética", modelo = "Sem foto", cor = "Azul",
            ano = 2026, combustivel = "Gasolina", capacidadeLitros = "50", grupoId = c.Dados.Grupo.Id,
        });
        Assert.AreEqual(201, (int)cadastro.StatusCode);
        using var frota = await cliente.GetAsync("/api/v1/veiculos");
        Assert.AreEqual(200, (int)frota.StatusCode);

        var comando = EstimativasSelecoesTests.Comando(c);
        var entrada = JsonSerializer.SerializeToNode(new
        {
            clienteId = comando.ClienteId, condutorId = comando.CondutorId, veiculoId = comando.VeiculoId,
            planoId = comando.PlanoId, retornoPrevisto = comando.RetornoPrevisto.ToString("O"),
            hodometroInicial = comando.HodometroInicial, taxasServicoIds = comando.TaxasServicoIds,
            seguros = comando.Seguros.Select(s => s.ToString()).ToArray(),
        })!.AsObject();
        using var criado = await EnviarAsync(cliente, HttpMethod.Post, "/api/v1/alugueis", entrada, chave: Guid.NewGuid().ToString());
        Assert.AreEqual(201, (int)criado.StatusCode);
        var id = (await ApiCliente.JsonAsync(criado)).GetProperty("id").GetGuid();
        var dados = await PersistenciaDevolucaoSqlTests.PrepararDadosAsync(c);
        var devolucao = Entrada(PreviaDevolucaoSqlTests.Comando(dados));
        var caminho = $"/api/v1/alugueis/{id}";
        using var previa = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao/previa", devolucao, criado.Headers.ETag!.ToString());
        Assert.AreEqual(200, (int)previa.StatusCode);
        devolucao["versoesAceitas"] = JsonNode.Parse((await ApiCliente.JsonAsync(previa)).GetProperty("versoesAceitas").GetRawText());
        using var final = await EnviarAsync(cliente, HttpMethod.Post, caminho + "/devolucao", devolucao,
            criado.Headers.ETag!.ToString(), Guid.NewGuid().ToString());
        Assert.AreEqual(200, (int)final.StatusCode);
        var demonstrativo = (await ApiCliente.JsonAsync(final)).GetProperty("devolucao").GetRawText();
        using var consulta = await cliente.GetAsync(caminho);
        Assert.AreEqual(demonstrativo, (await ApiCliente.JsonAsync(consulta)).GetProperty("devolucao").GetRawText());
        using var ocultacao = await EnviarAsync(cliente, HttpMethod.Delete, caminho, null, consulta.Headers.ETag!.ToString());
        Assert.AreEqual(204, (int)ocultacao.StatusCode);
        using var historico = await cliente.GetAsync(caminho + "?incluirOcultos=true");
        Assert.AreEqual(200, (int)historico.StatusCode);
        Assert.AreEqual(demonstrativo, (await ApiCliente.JsonAsync(historico)).GetProperty("devolucao").GetRawText());
        Assert.AreEqual(1, objetos.Chamadas, "As operações independentes não devem chamar o storage de fotos.");
    }

    private sealed class Relogio : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => CenarioLocacoesSql.Agora;
    }
    private sealed class Processador : IProcessadorImagem
    {
        public Task<ImagemSaneada> ProcessarAsync(Stream arquivo, string mime, CancellationToken token) =>
            Task.FromResult(new ImagemSaneada([1, 2, 3], "image/png", 1, 1));
    }
    private sealed class SemRetomada : IRetomadaFotos
    {
        public Task RetomarAsync(CancellationToken token) => Task.CompletedTask;
    }
    private sealed class Indisponiveis : IArmazenamentoObjeto
    {
        public int Chamadas { get; private set; }
        private ArmazenamentoObjetoException Falhar() { Chamadas++; return new(); }
        public Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken token) => throw Falhar();
        public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken token) => throw Falhar();
        public Task RemoverAsync(ObjetoFoto objeto, CancellationToken token) => throw Falhar();
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken token) => throw Falhar();
    }
}
