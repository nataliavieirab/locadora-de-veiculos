using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Transversal;

[TestClass]
public sealed class UnidadeTrabalhoEmpresaTests
{
    [TestMethod]
    public async Task CommitMantemLockAtePersistirELiberaDepois()
    {
        var empresaId = await CriarEmpresaAsync();
        await using var banco = CriarBanco(empresaId);
        var unidade = CriarUnidade(banco, empresaId);
        var id = Guid.NewGuid();

        var resultado = await unidade.ExecutarAsync(async cancelamento =>
        {
            Assert.IsNotNull(banco.Database.CurrentTransaction);
            await VerificarLockAsync(empresaId, bloqueado: true);
            banco.AuditoriasAcesso.Add(AuditoriaAcesso.Registrar(empresaId, id, DateTimeOffset.UtcNow));
            return id;
        }, CancellationToken.None);

        Assert.AreEqual(id, resultado);
        await using var leitura = CriarBanco(empresaId);
        Assert.IsTrue(await leitura.AuditoriasAcesso.AnyAsync(item => item.ContaId == id));
        await VerificarLockAsync(empresaId, bloqueado: false);
    }

    [TestMethod]
    public async Task FalhaDepoisDeSaveChangesDesfazTodaMutacaoELiberaLock()
    {
        var empresaId = await CriarEmpresaAsync();
        var id = Guid.NewGuid();
        await using var banco = CriarBanco(empresaId);
        var unidade = CriarUnidade(banco, empresaId);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            unidade.ExecutarAsync<int>(async cancelamento =>
            {
                banco.AuditoriasAcesso.Add(AuditoriaAcesso.Registrar(empresaId, id, DateTimeOffset.UtcNow));
                await banco.SaveChangesAsync(cancelamento);
                throw new InvalidOperationException("Falha controlada antes do commit.");
            }, CancellationToken.None));

        Assert.IsFalse(banco.ChangeTracker.HasChanges());
        await using var leitura = CriarBanco(empresaId);
        Assert.IsFalse(await leitura.AuditoriasAcesso.AnyAsync(item => item.ContaId == id));
        await VerificarLockAsync(empresaId, bloqueado: false);
    }

    [TestMethod]
    public async Task LockDeUmaEmpresaNaoBloqueiaOutra()
    {
        var empresaA = await CriarEmpresaAsync();
        var empresaB = await CriarEmpresaAsync();
        await using var bancoA = CriarBanco(empresaA);

        await CriarUnidade(bancoA, empresaA).ExecutarAsync(async cancelamento =>
        {
            await VerificarLockAsync(empresaA, bloqueado: true);
            await VerificarLockAsync(empresaB, bloqueado: false);
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task GuardaContinuaImpedindoEscritaDeOutraEmpresa()
    {
        var empresaA = await CriarEmpresaAsync();
        var empresaB = await CriarEmpresaAsync();
        await using var banco = CriarBanco(empresaA);

        await Assert.ThrowsExactlyAsync<EmpresaDivergenteException>(() =>
            CriarUnidade(banco, empresaA).ExecutarAsync(cancelamento =>
            {
                banco.AuditoriasAcesso.Add(AuditoriaAcesso.Registrar(empresaB, Guid.NewGuid(), DateTimeOffset.UtcNow));
                return Task.FromResult(true);
            }, CancellationToken.None));

        await VerificarLockAsync(empresaA, bloqueado: false);
    }

    [TestMethod]
    public async Task SemEmpresaOuEmpresaInexistenteNaoExecutaComando()
    {
        var executou = false;
        foreach (var empresa in new Guid?[] { null, Guid.Empty, Guid.NewGuid() })
        {
            var contexto = new ContextoEmpresa { EmpresaId = empresa };
            await using var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto);
            var unidade = new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);

            await Assert.ThrowsExactlyAsync<EmpresaDivergenteException>(() => unidade.ExecutarAsync(cancelamento =>
            {
                executou = true;
                return Task.FromResult(true);
            }, CancellationToken.None));
        }

        Assert.IsFalse(executou);
    }

    [TestMethod]
    public async Task CancelamentoDesfazMutacaoSemRepetirComando()
    {
        var empresaId = await CriarEmpresaAsync();
        await using var banco = CriarBanco(empresaId);
        using var cancelamento = new CancellationTokenSource();
        var chamadas = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CriarUnidade(banco, empresaId).ExecutarAsync<int>(async token =>
            {
                chamadas++;
                banco.AuditoriasAcesso.Add(AuditoriaAcesso.Registrar(empresaId, Guid.NewGuid(), DateTimeOffset.UtcNow));
                await banco.SaveChangesAsync(token);
                cancelamento.Cancel();
                token.ThrowIfCancellationRequested();
                return 0;
            }, cancelamento.Token));

        Assert.AreEqual(1, chamadas);
        await using var leitura = CriarBanco(empresaId);
        Assert.AreEqual(0, await leitura.AuditoriasAcesso.CountAsync());
        await VerificarLockAsync(empresaId, bloqueado: false);
    }

    [TestMethod]
    public async Task AlteracaoPendenteOuTransacaoAninhadaNaoEntraNaUnidade()
    {
        var empresaId = await CriarEmpresaAsync();
        await using var banco = CriarBanco(empresaId);
        var unidade = CriarUnidade(banco, empresaId);
        banco.AuditoriasAcesso.Add(AuditoriaAcesso.Registrar(empresaId, Guid.NewGuid(), DateTimeOffset.UtcNow));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            unidade.ExecutarAsync(token => Task.FromResult(true), CancellationToken.None));

        banco.ChangeTracker.Clear();
        await using var transacao = await banco.Database.BeginTransactionAsync();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            unidade.ExecutarAsync(token => Task.FromResult(true), CancellationToken.None));
    }

    private static LocadoraDbContext CriarBanco(Guid empresaId) =>
        AmbienteIntegracao.CriarContextoComGuarda(new ContextoEmpresa { EmpresaId = empresaId });

    private static UnidadeTrabalhoEmpresa CriarUnidade(LocadoraDbContext banco, Guid empresaId) =>
        new(banco, new ContextoEmpresa { EmpresaId = empresaId }, NullLogger<UnidadeTrabalhoEmpresa>.Instance);

    private static async Task<Guid> CriarEmpresaAsync()
    {
        var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true };
        await using var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto);
        var empresa = Empresa.Nova();
        banco.Empresas.Add(empresa);
        await banco.SaveChangesAsync();
        return empresa.Id;
    }

    private static async Task VerificarLockAsync(Guid empresaId, bool bloqueado)
    {
        await using var conexao = new SqlConnection(AmbienteIntegracao.Conexao);
        await conexao.OpenAsync();
        await using var transacao = (SqlTransaction)await conexao.BeginTransactionAsync();
        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = "SET LOCK_TIMEOUT 0; SELECT Id FROM Empresas WITH (UPDLOCK, HOLDLOCK) WHERE Id = @id;";
        comando.Parameters.AddWithValue("@id", empresaId);
        if (bloqueado)
        {
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(async () => await comando.ExecuteScalarAsync());
            Assert.AreEqual(1222, erro.Number);
        }
        else
        {
            Assert.AreEqual(empresaId, await comando.ExecuteScalarAsync());
        }
    }
}
