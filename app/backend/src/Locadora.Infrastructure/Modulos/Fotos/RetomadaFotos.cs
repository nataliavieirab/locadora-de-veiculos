using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Locadora.Infrastructure.Modulos.Fotos;

/// <summary>Uma passagem finita; sem timer, retry em loop ou ativação automática.</summary>
public sealed class RetomadaFotos(IServiceScopeFactory escopos, TimeProvider tempo) : IRetomadaFotos
{
    public async Task RetomarAsync(CancellationToken cancelamento)
    {
        using var descoberta = escopos.CreateScope();
        // Sem configuração de fotos não tocar SQL/storage nem degradar os cadastros.
        var destino = descoberta.ServiceProvider.GetService<DestinoFotos>();
        var objetos = descoberta.ServiceProvider.GetService<IArmazenamentoObjeto>();
        if (destino is null || objetos is null) return;
        var banco = descoberta.ServiceProvider.GetRequiredService<LocadoraDbContext>();
        var agora = tempo.GetUtcNow();
        var provedor = destino.Provedor.ToString();
        // Consulta transversal exclusiva de trabalho técnico: não desliga o filtro
        // de empresa genericamente, não devolve registros/imagens ao HTTP.
        var itens = await banco.Database.SqlQuery<ItemDescoberto>(
            $"SELECT TOP (10) t.Id, t.EmpresaId FROM TrabalhosObjetos t INNER JOIN FotosObjetos f ON f.EmpresaId=t.EmpresaId AND f.Id=t.FotoObjetoId WHERE t.Estado IN (0,1) AND (t.Tipo=0 OR (t.Tipo=1 AND f.Estado=2 AND f.ExcluirAposUtc<={agora})) AND t.ProximoProcessamentoUtc<={agora} AND (t.LeaseAteUtc IS NULL OR t.LeaseAteUtc<={agora}) AND f.Provedor={provedor} AND f.Bucket={destino.Bucket} ORDER BY t.ProximoProcessamentoUtc, t.Id")
            .ToListAsync(cancelamento);
        foreach (var item in itens)
        {
            cancelamento.ThrowIfCancellationRequested();
            using var escopo = escopos.CreateScope();
            var contexto = escopo.ServiceProvider.GetRequiredService<ContextoEmpresa>();
            contexto.EmpresaId = item.EmpresaId;
            var db = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
            var unidade = new UnidadeTrabalhoEmpresa(db, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
            var posse = await unidade.ExecutarAsync(async token =>
            {
                var trabalho = await db.TrabalhosObjetos.SingleOrDefaultAsync(t => t.Id == item.Id, token);
                if (trabalho is null
                    || trabalho.Estado is EstadoTrabalhoObjeto.Concluido or EstadoTrabalhoObjeto.FalhaFinal
                    || trabalho.LeaseAteUtc > tempo.GetUtcNow() || trabalho.ProximoProcessamentoUtc > tempo.GetUtcNow())
                    return null;
                var foto = await db.FotosObjetos.SingleAsync(f => f.Id == trabalho.FotoObjetoId, token);
                // Configuração mudou: nunca enviar chave de outro destino ao adaptador atual.
                if (foto.Provedor != destino.Provedor || foto.Bucket != destino.Bucket) return null;
                if (trabalho.Tipo == TipoTrabalhoObjeto.CompensarNuncaAtivo
                    ? foto.Estado is not (EstadoFoto.Pendente or EstadoFoto.Abandonada)
                    : !foto.ElegivelParaRemocao(tempo.GetUtcNow())) return null;
                var tokenPosse = trabalho.Adquirir(tempo.GetUtcNow(), foto);
                if (!await GuardaReferenciasFotos.PermiteAsync(db, foto, token))
                {
                    trabalho.RegistrarProtecao(tokenPosse, tempo.GetUtcNow());
                    return null;
                }
                if (foto.Estado == EstadoFoto.Pendente) foto.Abandonar(tempo.GetUtcNow());
                return new Posse(foto.Id, tokenPosse, CasosFotos.Objeto(foto),
                    trabalho.Tipo == TipoTrabalhoObjeto.RemocaoRetida || trabalho.EnvioConfirmado);
            }, cancelamento);
            if (posse is null) continue;
            var sucesso = false;
            try
            {
                // Sem ack, nem DELETE nem HEAD encerram a incerteza de um PUT tardio.
                if (posse.EnvioConfirmado)
                {
                    using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
                    limite.CancelAfter(TimeSpan.FromSeconds(30));
                    await objetos.RemoverAsync(posse.Objeto, limite.Token);
                    sucesso = await objetos.ConfirmarAusenciaAsync(posse.Objeto, limite.Token);
                }
            }
            catch (Exception e) when (e is ArmazenamentoObjetoException or IOException or OperationCanceledException)
            {
                if (cancelamento.IsCancellationRequested) throw;
            }
            await unidade.ExecutarAsync(async token =>
            {
                var trabalho = await db.TrabalhosObjetos.SingleAsync(t => t.Id == item.Id, token);
                // Outra retomada pode ter assumido posse após I/O demorado.
                if (trabalho.TokenPosse != posse.Token || trabalho.LeaseAteUtc <= tempo.GetUtcNow()) return false;
                var foto = await db.FotosObjetos.SingleAsync(f => f.Id == posse.FotoId, token);
                if (foto.Provedor != destino.Provedor || foto.Bucket != destino.Bucket
                    || !await GuardaReferenciasFotos.PermiteAsync(db, foto, token))
                {
                    trabalho.RegistrarProtecao(posse.Token, tempo.GetUtcNow());
                    return false;
                }
                if (sucesso && posse.EnvioConfirmado) trabalho.Concluir(posse.Token, foto, tempo.GetUtcNow());
                else trabalho.RegistrarFalha(posse.Token, tempo.GetUtcNow());
                return true;
            }, cancelamento);
        }
    }

    public sealed class ItemDescoberto
    {
        public Guid Id { get; set; }
        public Guid EmpresaId { get; set; }
    }
    private sealed record Posse(Guid FotoId, Guid Token, ObjetoFoto Objeto, bool EnvioConfirmado);
}

public sealed class RetomadaFotosStartup(IRetomadaFotos retomada, ILogger<RetomadaFotosStartup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try { await retomada.RetomarAsync(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Não expor SQL, destinos, chaves ou exceção bruta. A próxima operação
            // tentará uma nova passagem; fotografia não impede o host de iniciar.
            logger.LogWarning("Retomada de fotos indisponível na inicialização.");
        }
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
