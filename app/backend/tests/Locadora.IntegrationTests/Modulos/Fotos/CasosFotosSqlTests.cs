using Locadora.Application.Modulos.Fotos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Modulos.Locacoes;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.IntegrationTests.Modulos.Locacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
public sealed class CasosFotosSqlTests
{
    private static readonly ImagemSaneada Imagem = new([1, 2, 3], "image/png", 1, 1);
    private static string Etag(CenarioLocacoesSql c) => VersaoCadastro.Etag(c.Dados.Veiculo.RowVersion);

    [TestMethod]
    public async Task PendenciaExigeSessaoTenantVersaoEAluguelSemAlterarRaiz()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var (casos, _, _) = await CriarAsync(a);
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() =>
            casos.PrepararAsync(b.Dados.Veiculo.Id, Etag(b), Imagem, default));
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.PrepararAsync(a.Dados.Veiculo.Id, null, Imagem, default));
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() =>
            casos.PrepararAsync(a.Dados.Veiculo.Id, "\"AAAAAAAAAAA=\"", Imagem, default));
        var versao = a.Dados.Veiculo.RowVersion.ToArray();
        var pendencia = await casos.PrepararAsync(a.Dados.Veiculo.Id, Etag(a), Imagem, default);
        var atual = await a.Banco.Veiculos.AsNoTracking().SingleAsync();
        Assert.IsNull(atual.FotoAtivaId);
        CollectionAssert.AreEqual(versao, atual.RowVersion);
        var foto = await a.Banco.FotosObjetos.SingleAsync();
        var trabalho = await a.Banco.TrabalhosObjetos.SingleAsync();
        Assert.AreEqual(EstadoFoto.Pendente, foto.Estado);
        Assert.AreEqual(a.Contexto.EmpresaId!.Value.ToString("N") + "/" + foto.Id.ToString("N"), foto.Chave);
        Assert.AreEqual(pendencia.TokenPosse, trabalho.TokenPosse);
        Assert.AreEqual(EstadoTrabalhoObjeto.EmExecucao, trabalho.Estado);
        Assert.IsTrue(trabalho.LeaseAteUtc > DateTimeOffset.UtcNow);
        await PersistenciaDevolucaoSqlTests.CriarAbertoAsync(a);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            casos.PrepararAsync(a.Dados.Veiculo.Id, Etag(a), Imagem, default));
        Assert.AreEqual(1, await a.Banco.FotosObjetos.CountAsync());
    }

    [TestMethod]
    public async Task SessaoRevogadaERollbackDaCriacaoNaoDeixamPendencia()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, _, sessao) = await CriarAsync(c);
        var unidade = new FalhaAntesCommit(c);
        var comFalha = Montar(c, new SessaoFotos(c.Banco, c.Contexto, sessao.Id, 1, TimeProvider.System),
            new ObjetosControlados(c), unidade: unidade);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            comFalha.PrepararAsync(c.Dados.Veiculo.Id, Etag(c), Imagem, default));
        c.Banco.ChangeTracker.Clear();
        Assert.AreEqual(0, await c.Banco.FotosObjetos.CountAsync());
        Assert.AreEqual(0, await c.Banco.TrabalhosObjetos.CountAsync());
        var persistida = await c.Banco.SessoesAutenticacao.SingleAsync();
        persistida.Revogar(DateTimeOffset.UtcNow);
        await c.Banco.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<SessaoFotoInvalidaException>(() =>
            casos.PrepararAsync(c.Dados.Veiculo.Id, Etag(c), Imagem, default));
    }

    [TestMethod]
    public async Task DuasTrocasRetemObjetosAuditamEAlteramVersaoSemIoNaTransacao()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, objetos, _) = await CriarAsync(c);
        var primeira = await casos.EnviarAsync(c.Dados.Veiculo.Id, Etag(c), Stream.Null, "image/png", default);
        var idAnterior = primeira.FotoAtivaId;
        var versao = primeira.RowVersion.ToArray();
        var segunda = await casos.EnviarAsync(primeira.Id, VersaoCadastro.Etag(versao), Stream.Null, "image/png", default);
        Assert.AreNotEqual(idAnterior, segunda.FotoAtivaId);
        Assert.IsFalse(versao.SequenceEqual(segunda.RowVersion));
        Assert.AreEqual(2, objetos.Dados.Count);
        Assert.AreEqual(1, await c.Banco.FotosObjetos.CountAsync(f => f.Estado == EstadoFoto.Ativa));
        Assert.AreEqual(1, await c.Banco.FotosObjetos.CountAsync(f => f.Estado == EstadoFoto.Inativa));
        Assert.AreEqual(1, await c.Banco.TrabalhosObjetos.CountAsync(t => t.Estado == EstadoTrabalhoObjeto.Retido));
        var inativa = await c.Banco.FotosObjetos.SingleAsync(f => f.Estado == EstadoFoto.Inativa);
        var retencao = await c.Banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == inativa.Id);
        Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, retencao.Estado);
        Assert.AreEqual(inativa.RemovidaEmUtc!.Value.AddDays(30), inativa.ExcluirAposUtc);
        Assert.AreEqual(inativa.ExcluirAposUtc, retencao.ProximoProcessamentoUtc);
        Assert.AreEqual(2, await c.Banco.AuditoriasCadastro.CountAsync(a => a.CamposAlterados == "fotoAtivaId"));
    }

    [TestMethod]
    public async Task AuditoriaQueFalhaAposFlushDesfazTrocaSemApagarAnterior()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, objetos, sessao) = await CriarAsync(c);
        var primeira = await casos.EnviarAsync(c.Dados.Veiculo.Id, Etag(c), Stream.Null, "image/png", default);
        var idAnterior = primeira.FotoAtivaId;
        var versao = primeira.RowVersion.ToArray();
        var comFalha = Montar(c, new SessaoFotos(c.Banco, c.Contexto, sessao.Id, 1, TimeProvider.System), objetos,
            new AuditoriaFalha(c));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            comFalha.EnviarAsync(primeira.Id, VersaoCadastro.Etag(versao), Stream.Null, "image/png", default));
        c.Banco.ChangeTracker.Clear();
        var atual = await c.Banco.Veiculos.SingleAsync();
        Assert.AreEqual(idAnterior, atual.FotoAtivaId);
        CollectionAssert.AreEqual(versao, atual.RowVersion);
        Assert.AreEqual(2, objetos.Dados.Count);
        Assert.AreEqual(1, await c.Banco.FotosObjetos.CountAsync(f => f.Estado == EstadoFoto.Pendente));
        Assert.AreEqual(1, await c.Banco.AuditoriasCadastro.CountAsync());
        var anterior = await c.Banco.FotosObjetos.SingleAsync(f => f.Id == idAnterior);
        Assert.IsNull(anterior.RemovidaEmUtc);
        Assert.IsNull(anterior.ExcluirAposUtc);
        Assert.AreEqual(EstadoTrabalhoObjeto.Retido,
            (await c.Banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == idAnterior)).Estado);
    }

    [TestMethod]
    public async Task AlteracaoAluguelExclusaoESessaoNoIntervaloNegamAtivacao()
    {
        foreach (var acao in new[] { "edicao", "aluguel", "exclusao", "sessao", "lease" })
        {
            await using var c = await CenarioLocacoesSql.CriarAsync();
            var (casos, objetos, sessao) = await CriarAsync(c);
            objetos.AposGravar = async () =>
            {
                // Outra conexão e contexto, enquanto o objeto já existe e nenhuma
                // transação de upload segura o lock da empresa.
                await using var outroBanco = Locadora.IntegrationTests.Suporte.AmbienteIntegracao.CriarContextoComGuarda(c.Contexto);
                var outro = new CenarioLocacoesSql(c.Contexto, outroBanco, c.Dados);
                if (acao == "aluguel") await PersistenciaDevolucaoSqlTests.CriarAbertoAsync(outro);
                else if (acao == "sessao")
                {
                    var s = await outroBanco.SessoesAutenticacao.SingleAsync(s => s.Id == sessao.Id);
                    s.Revogar(DateTimeOffset.UtcNow);
                    await outroBanco.SaveChangesAsync();
                }
                else if (acao == "lease")
                {
                    // Simula vencimento de posse no SQL, sem alterar relógio do host.
                    await outroBanco.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE TrabalhosObjetos SET LeaseAteUtc={DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE EmpresaId={c.Contexto.EmpresaId}");
                }
                else
                {
                    var unidade = new UnidadeTrabalhoEmpresa(outroBanco, c.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
                    await unidade.ExecutarAsync(async token =>
                    {
                        var v = await outroBanco.Veiculos.SingleAsync(token);
                        if (acao == "exclusao") v.Excluir(DateTimeOffset.UtcNow);
                        else v.Alterar(new(v.Placa, v.Marca, "Prata", v.Modelo, v.Combustivel,
                            v.CapacidadeLitros, v.Ano, v.GrupoId), DateTimeOffset.UtcNow);
                        return true;
                    }, default);
                }
            };
            Exception? erro = null;
            try { await casos.EnviarAsync(c.Dados.Veiculo.Id, Etag(c), Stream.Null, "image/png", default); }
            catch (Exception e) { erro = e; }
            Assert.IsNotNull(erro, acao);
            Assert.IsTrue(erro is PrecondicaoCadastroException or ConflitoCadastroException
                or CadastroNaoEncontradoException or SessaoFotoInvalidaException or InvalidOperationException, acao);
            c.Banco.ChangeTracker.Clear();
            Assert.AreEqual(0, await c.Banco.FotosObjetos.CountAsync(f => f.Estado == EstadoFoto.Ativa), acao);
            Assert.AreEqual(1, objetos.Dados.Count, acao);
        }
    }

    private sealed class AuditoriaFalha(CenarioLocacoesSql c) : IAuditoriaCadastro
    {
        public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid id, params string[] campos)
        {
            new RegistroAuditoriaCadastro(c.Banco, c.Contexto, TimeProvider.System).Registrar(acao, entidade, id, campos);
            c.Banco.SaveChanges();
            throw new InvalidOperationException("Falha sintética da auditoria.");
        }
    }

    [TestMethod]
    public async Task FalhaAuditoriaNaRemocaoDesfazPrazoTrabalhoReferenciaEVersao()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, objetos, sessao) = await CriarAsync(c);
        var veiculo = await casos.EnviarAsync(c.Dados.Veiculo.Id, Etag(c), Stream.Null, "image/png", default);
        var id = veiculo.FotoAtivaId;
        var versao = veiculo.RowVersion.ToArray();
        var comFalha = Montar(c, new SessaoFotos(c.Banco, c.Contexto, sessao.Id, 1, TimeProvider.System),
            objetos, new AuditoriaFalha(c));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            comFalha.RemoverAsync(veiculo.Id, VersaoCadastro.Etag(versao), default));
        c.Banco.ChangeTracker.Clear();
        var atual = await c.Banco.Veiculos.SingleAsync();
        Assert.AreEqual(id, atual.FotoAtivaId);
        CollectionAssert.AreEqual(versao, atual.RowVersion);
        var foto = await c.Banco.FotosObjetos.SingleAsync();
        Assert.AreEqual(EstadoFoto.Ativa, foto.Estado);
        Assert.IsNull(foto.RemovidaEmUtc);
        Assert.IsNull(foto.ExcluirAposUtc);
        Assert.AreEqual(EstadoTrabalhoObjeto.Retido, (await c.Banco.TrabalhosObjetos.SingleAsync()).Estado);
        Assert.AreEqual(0, objetos.Remocoes);
    }

    [TestMethod]
    public async Task DoisEnviosConcorrentesComMesmaVersaoSomenteUmAtiva()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (primeiro, objetosA, sessao) = await CriarAsync(c);
        await using var bancoB = Locadora.IntegrationTests.Suporte.AmbienteIntegracao.CriarContextoComGuarda(c.Contexto);
        var b = new CenarioLocacoesSql(c.Contexto, bancoB, c.Dados);
        var objetosB = new ObjetosControlados(b);
        var segundo = Montar(b, new SessaoFotos(bancoB, c.Contexto, sessao.Id, 1, TimeProvider.System), objetosB);
        var ambos = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var chegaram = 0;
        Task Barreira()
        {
            if (Interlocked.Increment(ref chegaram) == 2) ambos.TrySetResult();
            return ambos.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        objetosA.AposGravar = Barreira;
        objetosB.AposGravar = Barreira;
        var versao = Etag(c);
        async Task<bool> Enviar(CasosFotos casos)
        {
            try { await casos.EnviarAsync(c.Dados.Veiculo.Id, versao, Stream.Null, "image/png", default); return true; }
            catch (PrecondicaoCadastroException) { return false; }
        }
        var resultados = await Task.WhenAll(Enviar(primeiro), Enviar(segundo));
        Assert.AreEqual(1, resultados.Count(r => r));
        c.Banco.ChangeTracker.Clear();
        Assert.AreEqual(1, await c.Banco.FotosObjetos.CountAsync(f => f.Estado == EstadoFoto.Ativa));
        Assert.AreEqual(1, await c.Banco.FotosObjetos.CountAsync(f => f.Estado == EstadoFoto.Pendente));
        Assert.AreEqual(1, await c.Banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(2, objetosA.Dados.Count + objetosB.Dados.Count);
    }

    [TestMethod]
    public async Task RemocaoLogicaEExclusaoVeiculoAgendamPrazoUmaVezSemIo()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, objetos, _) = await CriarAsync(c);
        var ativo = await casos.EnviarAsync(c.Dados.Veiculo.Id, Etag(c), Stream.Null, "image/png", default);
        var versaoAtiva = VersaoCadastro.Etag(ativo.RowVersion);
        var removido = await casos.RemoverAsync(ativo.Id, versaoAtiva, default);
        Assert.IsNull(removido.FotoAtivaId);
        Assert.AreNotEqual(versaoAtiva, VersaoCadastro.Etag(removido.RowVersion));
        var versaoRemovida = removido.RowVersion.ToArray();
        var fotoRemovida = await c.Banco.FotosObjetos.SingleAsync();
        var instante = fotoRemovida.RemovidaEmUtc;
        var piso = fotoRemovida.ExcluirAposUtc;
        await casos.RemoverAsync(ativo.Id, VersaoCadastro.Etag(versaoRemovida), default);
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() => casos.RemoverAsync(ativo.Id, versaoAtiva, default));
        c.Banco.ChangeTracker.Clear();
        CollectionAssert.AreEqual(versaoRemovida, (await c.Banco.Veiculos.SingleAsync()).RowVersion);
        Assert.AreEqual(instante, (await c.Banco.FotosObjetos.SingleAsync()).RemovidaEmUtc);
        Assert.AreEqual(piso, (await c.Banco.FotosObjetos.SingleAsync()).ExcluirAposUtc);
        var nova = await casos.EnviarAsync(ativo.Id, VersaoCadastro.Etag(versaoRemovida), Stream.Null, "image/png", default);
        var veiculos = new CasosVeiculos(new RepositorioVeiculos(c.Banco), new RepositorioGruposPlanos(c.Banco),
            new UnidadeTrabalhoEmpresa(c.Banco, c.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
            new RegistroAuditoriaCadastro(c.Banco, c.Contexto, TimeProvider.System), c.Contexto,
            new VinculosAluguelVeiculo(c.Banco), TimeProvider.System, new InativacaoFotoVeiculo(c.Banco));
        await veiculos.ExcluirAsync(nova.Id, VersaoCadastro.Etag(nova.RowVersion), default);
        Assert.AreEqual(0, await c.Banco.Veiculos.CountAsync());
        var historico = await c.Banco.Veiculos.IgnoreQueryFilters().SingleAsync(v => v.Id == nova.Id && v.EmpresaId == c.Contexto.EmpresaId);
        Assert.IsNull(historico.FotoAtivaId);
        Assert.AreEqual(2, await c.Banco.FotosObjetos.CountAsync(f => f.Estado == EstadoFoto.Inativa));
        Assert.AreEqual(2, await c.Banco.TrabalhosObjetos.CountAsync(t => t.Estado == EstadoTrabalhoObjeto.Pendente));
        foreach (var foto in await c.Banco.FotosObjetos.ToListAsync())
        {
            Assert.AreEqual(foto.RemovidaEmUtc!.Value.AddDays(30), foto.ExcluirAposUtc);
            Assert.AreEqual(foto.ExcluirAposUtc,
                (await c.Banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == foto.Id)).ProximoProcessamentoUtc);
        }
        Assert.AreEqual(2, objetos.Dados.Count);
    }

    [TestMethod]
    public async Task RemocaoValidaVersaoAluguelETenantMesmoSemReferencia()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var (casos, _, _) = await CriarAsync(c);
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() => casos.RemoverAsync(c.Dados.Veiculo.Id, null, default));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => casos.RemoverAsync(Guid.NewGuid(), Etag(c), default));
        await PersistenciaDevolucaoSqlTests.CriarAbertoAsync(c);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => casos.RemoverAsync(c.Dados.Veiculo.Id, Etag(c), default));
    }

    internal static CasosFotos Montar(CenarioLocacoesSql c, ISessaoFotos sessao, ObjetosControlados objetos,
        IAuditoriaCadastro? auditoria = null, IUnidadeTrabalhoEmpresa? unidade = null, IRetomadaFotos? retomada = null) =>
        new(new RepositorioVeiculos(c.Banco), new RepositorioFotos(c.Banco), new VinculosAluguelVeiculo(c.Banco),
            unidade ?? new UnidadeTrabalhoEmpresa(c.Banco, c.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
            sessao, new ProcessadorControlado(), objetos, new(ProvedorFoto.Local, objetos.Bucket),
            auditoria ?? new RegistroAuditoriaCadastro(c.Banco, c.Contexto, TimeProvider.System), TimeProvider.System, retomada);

    internal static async Task<(CasosFotos, ObjetosControlados, SessaoAutenticacao)> CriarAsync(CenarioLocacoesSql c)
    {
        var sessao = SessaoAutenticacao.Abrir(c.Contexto.ContaId!.Value, c.Contexto.EmpresaId!.Value, DateTimeOffset.UtcNow);
        c.Banco.Add(sessao);
        await c.Banco.SaveChangesAsync();
        var objetos = new ObjetosControlados(c);
        return (Montar(c, new SessaoFotos(c.Banco, c.Contexto, sessao.Id, 1, TimeProvider.System), objetos), objetos, sessao);
    }

    internal sealed class ObjetosControlados(CenarioLocacoesSql c) : IArmazenamentoObjeto
    {
        public string Bucket { get; } = "ensaio-" + Guid.NewGuid().ToString("N");
        public Dictionary<string, byte[]> Dados { get; } = [];
        public Func<Task>? AposGravar { get; set; }
        public Func<Task>? AntesRemover { get; set; }
        public int Remocoes { get; private set; }
        public bool FalharRemocao { get; set; }
        public bool FalharLeitura { get; set; }
        public async Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken cancelamento)
        {
            Assert.IsNull(c.Banco.Database.CurrentTransaction);
            Dados[objeto.Chave] = bytes.ToArray();
            if (AposGravar is not null) await AposGravar();
        }
        public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken cancelamento)
        {
            Assert.IsNull(c.Banco.Database.CurrentTransaction);
            if (FalharLeitura) throw new ArmazenamentoObjetoException();
            return Task.FromResult(Dados[objeto.Chave]);
        }
        public async Task RemoverAsync(ObjetoFoto objeto, CancellationToken cancelamento)
        {
            Assert.IsNull(c.Banco.Database.CurrentTransaction);
            Remocoes++;
            if (AntesRemover is not null) await AntesRemover();
            if (FalharRemocao) throw new ArmazenamentoObjetoException();
            Dados.Remove(objeto.Chave);
        }
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken cancelamento) =>
            Task.FromResult(!Dados.ContainsKey(objeto.Chave));
    }

    private sealed class ProcessadorControlado : IProcessadorImagem
    {
        public Task<ImagemSaneada> ProcessarAsync(Stream arquivo, string mime, CancellationToken cancelamento) => Task.FromResult(Imagem);
    }

    private sealed class FalhaAntesCommit(CenarioLocacoesSql c) : IUnidadeTrabalhoEmpresa
    {
        public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento)
        {
            c.Banco.ChangeTracker.Clear();
            await using var tx = await c.Banco.Database.BeginTransactionAsync(cancelamento);
            await comando(cancelamento);
            await c.Banco.SaveChangesAsync(cancelamento);
            await tx.RollbackAsync(cancelamento);
            c.Banco.ChangeTracker.Clear();
            throw new InvalidOperationException("Falha sintética antes do commit.");
        }
    }
}
