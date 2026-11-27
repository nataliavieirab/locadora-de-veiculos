using System.Net;
using System.Runtime.Versioning;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Locadora.Application.Modulos.Fotos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Ensaios.FotosR2.Retencao;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Locadora.Ensaios.FotosR2;

// Preparado no bloco 6, nunca chamado pela suíte nem pela qualificação offline.
[SupportedOSPlatform("linux")]
internal static class EntradaRetencao
{
    private const string Bucket = "locadora-veiculos-dev";
    public static async Task<int> ExecutarAsync(string backend)
    {
        var etapa = "autorizacao";
        var iniciouCatalogo = false;
        try
        {
            Exigir(Environment.GetEnvironmentVariable("LOCADORA_R2_RETENCAO_AUTORIZADA") == "NOVOS_OBJETOS");
            etapa = "credenciais-locais";
            var segredos = new ConfigurationBuilder().AddUserSecrets("locadora-de-veiculos-dev").Build();
            if (string.IsNullOrWhiteSpace(segredos["Ensaios:FotosR2:SomenteLeitura:AccessKey"])
                || string.IsNullOrWhiteSpace(segredos["Ensaios:FotosR2:SomenteLeitura:SecretKey"]))
            {
                Console.Error.WriteLine("BLOCKED: configure localmente Ensaios:FotosR2:SomenteLeitura:AccessKey e SecretKey com Object Read only restrito ao bucket dev; não envie valores no chat.");
                return 2;
            }
            Exigir(segredos["Fotos:Provedor"] == "R2" && segredos["Fotos:R2:Bucket"] == Bucket
                && segredos.GetValue<bool>("Fotos:R2:BucketPrivadoConfirmado"));
            var endpoint = new Uri(segredos["Fotos:R2:Endpoint"] ?? "");
            ConfiguracaoR2 Configurar(bool leitura) => new()
            {
                Endpoint = endpoint.AbsoluteUri, Bucket = Bucket, BucketPrivadoConfirmado = true,
                AccessKey = segredos[leitura ? "Ensaios:FotosR2:SomenteLeitura:AccessKey" : "Fotos:R2:AccessKey"] ?? "",
                SecretKey = segredos[leitura ? "Ensaios:FotosR2:SomenteLeitura:SecretKey" : "Fotos:R2:SecretKey"] ?? "",
            };
            var normal = Configurar(false);
            var leitura = Configurar(true);
            normal.Validar();
            leitura.Validar();
            etapa = "catalogo-novo";
            // Sempre CREATE DATABASE aleatório; nunca migrar o catálogo da configuração.
            await AmbienteIntegracao.Iniciar(null!);
            iniciouCatalogo = true;
            var catalogo = new SqlConnectionStringBuilder(AmbienteIntegracao.Conexao).InitialCatalog;
            var tempo = new RelogioRetencao();
            var manifesto = ManifestoRetencao.Criar(backend, catalogo, endpoint, tempo);
            Console.WriteLine("Manifesto privado novo: " + Path.GetRelativePath(backend, manifesto.Caminho));
            var opcoes = normal.CriarOpcoes();
            opcoes.HttpClientFactory = new FabricaHttpRetencao(manifesto);
            var opcoesLeitura = leitura.CriarOpcoes();
            opcoesLeitura.HttpClientFactory = new FabricaHttpRetencao(manifesto);
            using var s3 = new AmazonS3Client(new BasicAWSCredentials(segredos["Fotos:R2:AccessKey"], segredos["Fotos:R2:SecretKey"]), opcoes);
            using var s3Leitura = new AmazonS3Client(new BasicAWSCredentials(
                segredos["Ensaios:FotosR2:SomenteLeitura:AccessKey"], segredos["Ensaios:FotosR2:SomenteLeitura:SecretKey"]), opcoesLeitura);
            var objetos = new ObjetosRetencao(new ArmazenamentoR2(s3, normal), manifesto);
            var empresa = Empresa.Nova();
            var nome = "retencao_" + Guid.NewGuid().ToString("N");
            var conta = new UsuarioConta { Id = Guid.NewGuid(), EmpresaId = empresa.Id,
                UserName = nome, NormalizedUserName = nome.ToUpperInvariant() };
            var contexto = new ContextoEmpresa { EmpresaId = empresa.Id, ContaId = conta.Id,
                CorrelacaoId = Guid.NewGuid(), PermiteCriarEmpresa = true };
            await using var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto);
            var grupo = Grupo.Novo(empresa.Id, "Grupo sintético", tempo.GetUtcNow());
            var veiculo = Veiculo.Novo(empresa.Id, new("FIC1A01", "Sintética", "Azul", "Retenção R2",
                TipoCombustivel.Gasolina, 50, 2026, grupo.Id), tempo.GetUtcNow());
            var sessao = SessaoAutenticacao.Abrir(conta.Id, empresa.Id, DateTimeOffset.UtcNow);
            banco.AddRange(empresa, conta, grupo, veiculo, sessao);
            await banco.SaveChangesAsync();
            contexto.PermiteCriarEmpresa = false;
            manifesto.AutorizarEmpresa(empresa.Id);
            async Task AtualizarAsync()
            {
                // Contexto novo e empresa explícita, sem tracking residual e sem
                // usar configuração de catálogo de Development.
                await using var consulta = AmbienteIntegracao.CriarContexto(AmbienteIntegracao.Conexao,
                    new ContextoEmpresa { EmpresaId = empresa.Id });
                foreach (var foto in await consulta.FotosObjetos.AsNoTracking().ToListAsync())
                {
                    var trabalho = await consulta.TrabalhosObjetos.AsNoTracking().SingleAsync(t => t.FotoObjetoId == foto.Id);
                    var semReferencia = !await consulta.Veiculos.IgnoreQueryFilters()
                        .AnyAsync(v => v.EmpresaId == empresa.Id && v.FotoAtivaId == foto.Id);
                    manifesto.Atualizar(foto, trabalho, semReferencia);
                }
            }
            var atualizados = new AtualizacaoAntesDeRemover(objetos, AtualizarAsync);
            etapa = "processador-local";
            var atual = File.ReadAllLines("/proc/self/cgroup").Single(l => l.StartsWith("0::", StringComparison.Ordinal))[3..];
            var cgroups = Directory.GetParent(Path.Combine("/sys/fs/cgroup", atual.TrimStart('/')))!.FullName;
            var temporarios = Path.Combine(Path.GetDirectoryName(manifesto.Caminho)!, "temporarios");
            Directory.CreateDirectory(temporarios, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var processador = new ProcessadorImagemIsolado(new(
                File.ResolveLinkTarget("/usr/bin/dotnet", true)!.FullName,
                Path.Combine(backend, "src/Locadora.ProcessadorImagens/bin/Debug/net10.0/Locadora.ProcessadorImagens.dll"),
                cgroups, temporarios));
            using var bitmap = new SKBitmap(32, 24);
            bitmap.Erase(SKColors.CornflowerBlue);
            using var imagem = SKImage.FromBitmap(bitmap);
            using var png = imagem.Encode(SKEncodedImageFormat.Png, 90);
            var bytes = png.ToArray();
            var saneada = await processador.ProcessarAsync(new MemoryStream(bytes), "image/png", default);
            var unidade = new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
            var casos = new CasosFotos(new RepositorioVeiculos(banco), new RepositorioFotos(banco),
                new VinculosAluguelVeiculo(banco), unidade,
                new SessaoFotos(banco, contexto, sessao.Id, 1, TimeProvider.System), processador, atualizados,
                new(ProvedorFoto.R2, Bucket), new RegistroAuditoriaCadastro(banco, contexto, tempo), tempo);
            async Task<Veiculo> ConsultarAsync()
            {
                banco.ChangeTracker.Clear();
                return await banco.Veiculos.SingleAsync();
            }
            async Task EnviarAsync()
            {
                var v = await ConsultarAsync();
                await casos.EnviarAsync(v.Id, VersaoCadastro.Etag(v.RowVersion), new MemoryStream(bytes), "image/png", default);
                await AtualizarAsync();
            }
            async Task RetomarAsync()
            {
                var servicos = new ServiceCollection();
                servicos.AddScoped<ContextoEmpresa>();
                servicos.AddScoped(p => AmbienteIntegracao.CriarContextoComGuarda(
                    p.GetRequiredService<ContextoEmpresa>(), AmbienteIntegracao.Conexao));
                servicos.AddSingleton(new DestinoFotos(ProvedorFoto.R2, Bucket));
                servicos.AddSingleton<IArmazenamentoObjeto>(atualizados);
                using var provedor = servicos.BuildServiceProvider(validateScopes: true);
                await new RetomadaFotos(provedor.GetRequiredService<IServiceScopeFactory>(), tempo).RetomarAsync(default);
                await AtualizarAsync();
            }
            etapa = "put-get-novo";
            await EnviarAsync();
            var primeira = await banco.FotosObjetos.AsNoTracking().SingleAsync();
            var objetoPrimeiro = CasosFotos.Objeto(primeira);
            Exigir((await objetos.LerAsync(objetoPrimeiro, default)).SequenceEqual(saneada.Bytes));
            manifesto.RegistrarVerificacao("put-get-novo-hash-conferido");
            etapa = "permissao-somente-leitura";
            manifesto.ConferirLeitura(objetoPrimeiro);
            Exigir((await new ArmazenamentoR2(s3Leitura, leitura).LerAsync(objetoPrimeiro, default)).SequenceEqual(saneada.Bytes));
            await ExigirErroPutAsync(s3Leitura, objetoPrimeiro, saneada.Bytes, HttpStatusCode.Forbidden, "AccessDenied");
            manifesto.RegistrarVerificacao("somente-leitura-get-permitido-put-403-accessdenied");
            etapa = "put-condicional-412";
            await ExigirErroPutAsync(s3, objetoPrimeiro, saneada.Bytes, HttpStatusCode.PreconditionFailed, "PreconditionFailed");
            Exigir((await objetos.LerAsync(objetoPrimeiro, default)).SequenceEqual(saneada.Bytes));
            manifesto.RegistrarVerificacao("put-condicional-412-bytes-intactos");
            etapa = "ativa-antiga";
            tempo.Agora = tempo.Agora.AddDays(90);
            await RetomarAsync();
            Exigir(objetos.Remocoes == 0);
            manifesto.RegistrarVerificacao("ativa-antiga-sem-delete");
            etapa = "troca-retencao";
            await EnviarAsync();
            banco.ChangeTracker.Clear();
            primeira = await banco.FotosObjetos.AsNoTracking().SingleAsync(f => f.Id == primeira.Id);
            tempo.Agora = primeira.ExcluirAposUtc!.Value.AddTicks(-1);
            await RetomarAsync();
            Exigir(objetos.Remocoes == 0 && !await objetos.ConfirmarAusenciaAsync(objetoPrimeiro, default));
            manifesto.RegistrarVerificacao("inativa-antes-piso-sem-delete");
            tempo.Agora = primeira.ExcluirAposUtc.Value;
            await RetomarAsync();
            Exigir(objetos.Remocoes == 1 && await objetos.ConfirmarAusenciaAsync(objetoPrimeiro, default));
            manifesto.RegistrarAusencia(objetoPrimeiro);
            manifesto.RegistrarVerificacao("inativa-no-piso-delete-head-ausente");
            await RetomarAsync();
            Exigir(objetos.Remocoes == 1);
            manifesto.RegistrarVerificacao("passagem-repetida-sem-delete-extra");
            etapa = "remocao-retencao";
            var vAtual = await ConsultarAsync();
            var segundaId = vAtual.FotoAtivaId!.Value;
            await casos.RemoverAsync(vAtual.Id, VersaoCadastro.Etag(vAtual.RowVersion), default);
            await AtualizarAsync();
            banco.ChangeTracker.Clear();
            var segunda = await banco.FotosObjetos.AsNoTracking().SingleAsync(f => f.Id == segundaId);
            tempo.Agora = segunda.ExcluirAposUtc!.Value;
            await RetomarAsync();
            Exigir(await objetos.ConfirmarAusenciaAsync(CasosFotos.Objeto(segunda), default));
            manifesto.RegistrarAusencia(CasosFotos.Objeto(segunda));
            manifesto.RegistrarVerificacao("remocao-logica-retencao-delete-head-ausente");
            etapa = "compensacao-nunca-ativo";
            vAtual = await ConsultarAsync();
            var pendencia = await casos.PrepararAsync(vAtual.Id, VersaoCadastro.Etag(vAtual.RowVersion), saneada, default);
            await objetos.GravarAsync(pendencia.Objeto, saneada.Bytes, default);
            await unidade.ExecutarAsync(async token =>
            {
                var trabalho = await banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == pendencia.FotoId, token);
                trabalho.ConfirmarEnvio(pendencia.TokenPosse, tempo.GetUtcNow());
                return true;
            }, default);
            tempo.Agora = tempo.Agora.AddMinutes(3);
            await RetomarAsync();
            Exigir(await objetos.ConfirmarAusenciaAsync(pendencia.Objeto, default));
            manifesto.RegistrarAusencia(pendencia.Objeto);
            manifesto.RegistrarVerificacao("nunca-ativo-compensacao-delete-head-ausente");
            banco.ChangeTracker.Clear();
            Exigir(await banco.FotosObjetos.CountAsync() == 3 && await banco.TrabalhosObjetos.CountAsync(t => t.Estado == EstadoTrabalhoObjeto.Concluido) == 3);
            Console.WriteLine("PASS recorte R2 real: permissão/412/retenção/compensação de três objetos NOVOS. Catálogo e manifesto preservados; não encerra automaticamente 8.3/F5.");
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("FAIL etapa fixa: " + etapa + ". Detalhes suprimidos; catálogo, manifesto e pendências preservados.");
            return 1;
        }
        finally
        {
            if (iniciouCatalogo) await AmbienteIntegracao.Fabrica.DisposeAsync();
            // Não chamar AssemblyCleanup, não apagar catálogo nem limpar objetos.
        }
    }

    private static async Task ExigirErroPutAsync(IAmazonS3 cliente, ObjetoFoto objeto, byte[] bytes,
        HttpStatusCode esperado, string codigo)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        try
        {
            await cliente.PutObjectAsync(new PutObjectRequest
            {
                BucketName = Bucket, Key = objeto.Chave, ContentType = objeto.Mime, InputStream = stream,
                AutoCloseStream = false, UseChunkEncoding = false, DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true, IfNoneMatch = "*",
            });
        }
        catch (AmazonS3Exception erro) when (erro.StatusCode == esperado && erro.ErrorCode == codigo) { return; }
        throw new InvalidOperationException("Prova remota não obtida.");
    }
    private static void Exigir(bool condicao) { if (!condicao) throw new InvalidOperationException(); }
    private sealed class RelogioRetencao : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Agora;
    }
    private sealed class AtualizacaoAntesDeRemover(IArmazenamentoObjeto objetos, Func<Task> atualizar) : IArmazenamentoObjeto
    {
        public Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken token) => objetos.GravarAsync(objeto, bytes, token);
        public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken token) => objetos.LerAsync(objeto, token);
        public async Task RemoverAsync(ObjetoFoto objeto, CancellationToken token)
        {
            await atualizar();
            await objetos.RemoverAsync(objeto, token);
        }
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken token) => objetos.ConfirmarAusenciaAsync(objeto, token);
    }
}
