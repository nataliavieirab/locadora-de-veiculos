using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Transversal;

[TestClass]
public sealed class SchemaCadastrosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task UpgradeDaF1MantemEmpresaContaESessao()
    {
        var conexao = await AmbienteIntegracao.CriarCatalogoVazioAsync();
        try
        {
            var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true };
            await using var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto, conexao);
            await banco.GetService<IMigrator>().MigrateAsync("20261120214341_Inicial");
            var empresa = Empresa.Nova();
            var conta = Conta(empresa.Id);
            contexto.EmpresaId = empresa.Id;
            var sessao = SessaoAutenticacao.Abrir(conta.Id, empresa.Id, Agora);
            banco.AddRange(empresa, conta, sessao);
            await banco.SaveChangesAsync();

            await banco.Database.MigrateAsync();
            banco.ChangeTracker.Clear();

            Assert.IsTrue(await banco.Empresas.AnyAsync(item => item.Id == empresa.Id));
            Assert.IsTrue(await banco.Users.AnyAsync(item => item.Id == conta.Id));
            Assert.IsTrue(await banco.SessoesAutenticacao.AnyAsync(item => item.Id == sessao.Id));
            var grupo = Grupo.Novo(empresa.Id, "Grupo após upgrade", Agora);
            banco.Grupos.Add(grupo);
            await banco.SaveChangesAsync();
            Assert.AreEqual(8, grupo.RowVersion.Length);
            Assert.IsFalse((await banco.Database.GetPendingMigrationsAsync()).Any());
            Assert.IsFalse(banco.Database.HasPendingModelChanges());
        }
        finally
        {
            await AmbienteIntegracao.RemoverCatalogoAsync(conexao);
        }
    }

    [TestMethod]
    public async Task UpgradeDoIndiceDePlanosPreservaExcluidosERejeitaDuplicidadeAtiva()
    {
        var conexao = await AmbienteIntegracao.CriarCatalogoVazioAsync();
        try
        {
            var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true };
            await using var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto, conexao);
            await banco.GetService<IMigrator>().MigrateAsync("20261123022306_CadastrosFase2");
            var empresa = Empresa.Nova();
            contexto.EmpresaId = empresa.Id;
            var grupo = Grupo.Novo(empresa.Id, "Preservado no upgrade", Agora);
            var anterior = Plano.Novo(empresa.Id, grupo.Id, ModalidadePlano.Livre, 100m, null, null, null, Agora);
            banco.AddRange(empresa, grupo, anterior);
            await banco.SaveChangesAsync();
            anterior.Excluir(Agora.AddMinutes(1));
            await banco.SaveChangesAsync();
            var versaoAnterior = anterior.RowVersion.ToArray();

            await banco.Database.MigrateAsync();
            banco.ChangeTracker.Clear();
            var historico = await banco.Planos.IgnoreQueryFilters()
                .SingleAsync(item => item.EmpresaId == empresa.Id && item.Id == anterior.Id);
            Assert.AreEqual(100m, historico.PrecoDiario);
            Assert.AreEqual(Agora.AddMinutes(1), historico.ExcluidoEmUtc);
            CollectionAssert.AreEqual(versaoAnterior, historico.RowVersion);
            var novo = Plano.Novo(empresa.Id, grupo.Id, ModalidadePlano.Livre, 120m, null, null, null, Agora.AddMinutes(2));
            banco.Add(novo);
            await banco.SaveChangesAsync();
            Assert.AreNotEqual(anterior.Id, novo.Id);
            Assert.AreEqual(1, await banco.Planos.CountAsync());
            Assert.AreEqual(2, await banco.Planos.IgnoreQueryFilters().CountAsync(item => item.EmpresaId == empresa.Id));

            banco.Add(Plano.Novo(empresa.Id, grupo.Id, ModalidadePlano.Livre, 150m, null, null, null, Agora.AddMinutes(3)));
            await DuplicidadeAsync(banco);
        }
        finally
        {
            await AmbienteIntegracao.RemoverCatalogoAsync(conexao);
        }
    }

    [TestMethod]
    public async Task TodasAsEntidadesSaoIsoladasEFiltrosDeAtividadeNaoDesligamEmpresa()
    {
        var a = await CriarEmpresaAsync();
        var b = await CriarEmpresaAsync();
        await using var bancoA = Banco(a);
        await using var bancoB = Banco(b);
        await PopularAsync(bancoA, a);
        await PopularAsync(bancoB, b);

        await VerificarEmpresaAsync<Grupo>(bancoA, a.EmpresaId);
        await VerificarEmpresaAsync<Plano>(bancoA, a.EmpresaId);
        await VerificarEmpresaAsync<Veiculo>(bancoA, a.EmpresaId);
        await VerificarEmpresaAsync<Cliente>(bancoA, a.EmpresaId);
        await VerificarEmpresaAsync<Condutor>(bancoA, a.EmpresaId);
        await VerificarEmpresaAsync<VinculoCliente>(bancoA, a.EmpresaId);
        await VerificarEmpresaAsync<TaxaServico>(bancoA, a.EmpresaId);
        await VerificarEmpresaAsync<PrecoSeguro>(bancoA, a.EmpresaId);
        await VerificarEmpresaAsync<PrecoCombustivel>(bancoA, a.EmpresaId);
        await VerificarEmpresaAsync<AuditoriaCadastro>(bancoA, a.EmpresaId);

        var grupo = await bancoA.Grupos.SingleAsync();
        grupo.Excluir(Agora.AddMinutes(1));
        await bancoA.SaveChangesAsync();
        Assert.AreEqual(0, await bancoA.Grupos.CountAsync());
        Assert.AreEqual(1, await bancoB.Grupos.CountAsync());
        Assert.AreEqual(1, await bancoA.Grupos.IgnoreQueryFilters().CountAsync(item => item.EmpresaId == a.EmpresaId));

        await using var semEmpresa = AmbienteIntegracao.CriarContexto();
        Assert.AreEqual(0, await semEmpresa.Grupos.CountAsync());
        Assert.AreEqual(0, await semEmpresa.Clientes.CountAsync());
        Assert.AreEqual(0, await semEmpresa.PrecosCombustiveis.CountAsync());
    }

    [TestMethod]
    public async Task RowversionMudaEAAtualizacaoObsoletaFalha()
    {
        var contexto = await CriarEmpresaAsync();
        await using var primeiro = Banco(contexto);
        var grupo = Grupo.Novo(contexto.EmpresaId!.Value, "Inicial", Agora);
        primeiro.Grupos.Add(grupo);
        await primeiro.SaveChangesAsync();
        var versao = grupo.RowVersion.ToArray();
        await using var segundo = Banco(contexto);
        var obsoleto = await segundo.Grupos.SingleAsync();

        grupo.Alterar("Atualizado", Agora.AddMinutes(1));
        await primeiro.SaveChangesAsync();

        Assert.IsFalse(versao.SequenceEqual(grupo.RowVersion));
        obsoleto.Alterar("Tentativa antiga", Agora.AddMinutes(2));
        await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => segundo.SaveChangesAsync());
    }

    [TestMethod]
    public async Task PlacaUnicaSomenteEntreAtivosPermiteNovaIdentidadeEAOutraEmpresa()
    {
        var a = await CriarEmpresaAsync();
        var b = await CriarEmpresaAsync();
        await using var bancoA = Banco(a);
        await using var bancoB = Banco(b);
        var grupoA = Grupo.Novo(a.EmpresaId!.Value, "Grupo A", Agora);
        var grupoB = Grupo.Novo(b.EmpresaId!.Value, "Grupo B", Agora);
        bancoA.Grupos.Add(grupoA);
        bancoB.Grupos.Add(grupoB);
        await bancoA.SaveChangesAsync();
        await bancoB.SaveChangesAsync();
        var primeiro = Veiculo.Novo(a.EmpresaId.Value, DadosVeiculo(grupoA.Id), Agora);
        bancoA.Veiculos.Add(primeiro);
        await bancoA.SaveChangesAsync();

        bancoA.Veiculos.Add(Veiculo.Novo(a.EmpresaId.Value, DadosVeiculo(grupoA.Id) with { Placa = "abc1d23" }, Agora));
        await DuplicidadeAsync(bancoA);
        bancoA.ChangeTracker.Clear();
        var original = await bancoA.Veiculos.SingleAsync();
        original.Excluir(Agora.AddMinutes(1));
        await bancoA.SaveChangesAsync();
        var novo = Veiculo.Novo(a.EmpresaId.Value, DadosVeiculo(grupoA.Id), Agora.AddMinutes(2));
        bancoA.Veiculos.Add(novo);
        bancoB.Veiculos.Add(Veiculo.Novo(b.EmpresaId.Value, DadosVeiculo(grupoB.Id), Agora));
        await bancoA.SaveChangesAsync();
        await bancoB.SaveChangesAsync();
        Assert.AreNotEqual(primeiro.Id, novo.Id);
        Assert.AreEqual(1, await bancoA.Veiculos.CountAsync());
        Assert.AreEqual(2, await bancoA.Veiculos.IgnoreQueryFilters().CountAsync(item => item.EmpresaId == a.EmpresaId));
    }

    [TestMethod]
    public async Task DocumentosSaoUnicosPorPapelEntreAtivosESaoReutilizaveis()
    {
        var a = await CriarEmpresaAsync();
        var b = await CriarEmpresaAsync();
        await using var banco = Banco(a);
        await using var outro = Banco(b);
        var empresaId = a.EmpresaId!.Value;
        var pf = Cliente.Novo(empresaId, Pf(), Agora);
        var pj = Cliente.Novo(empresaId, Pj(), Agora);
        banco.AddRange(pf, pj);
        await banco.SaveChangesAsync();
        var condutor = Condutor.Novo(empresaId, DadosCondutor(pf.Id), Agora);
        banco.Add(condutor);
        await banco.SaveChangesAsync();
        Assert.AreEqual(pf.CpfNormalizado, condutor.CpfNormalizado);

        banco.Add(Cliente.Novo(empresaId, Pf(), Agora));
        await DuplicidadeAsync(banco);
        banco.ChangeTracker.Clear();
        banco.Add(Cliente.Novo(empresaId, Pj(), Agora));
        await DuplicidadeAsync(banco);
        banco.ChangeTracker.Clear();
        banco.Add(Condutor.Novo(empresaId, DadosCondutor(pf.Id), Agora));
        await DuplicidadeAsync(banco);
        banco.ChangeTracker.Clear();

        var pfAnterior = await banco.Clientes.SingleAsync(item => item.Id == pf.Id);
        var pjAnterior = await banco.Clientes.SingleAsync(item => item.Id == pj.Id);
        var condutorAnterior = await banco.Condutores.SingleAsync();
        pfAnterior.Excluir(Agora.AddMinutes(1));
        pjAnterior.Excluir(Agora.AddMinutes(1));
        condutorAnterior.Excluir(Agora.AddMinutes(1));
        await banco.SaveChangesAsync();
        var novoPf = Cliente.Novo(empresaId, Pf(), Agora.AddMinutes(2));
        var novoPj = Cliente.Novo(empresaId, Pj(), Agora.AddMinutes(2));
        var novoCondutor = Condutor.Novo(empresaId, DadosCondutor(novoPf.Id), Agora.AddMinutes(2));
        banco.AddRange(novoPf, novoPj, novoCondutor);
        await banco.SaveChangesAsync();
        Assert.AreNotEqual(pf.Id, novoPf.Id);
        Assert.AreNotEqual(pj.Id, novoPj.Id);
        Assert.AreNotEqual(condutor.Id, novoCondutor.Id);

        var pfB = Cliente.Novo(b.EmpresaId!.Value, Pf(), Agora);
        outro.AddRange(pfB, Cliente.Novo(b.EmpresaId.Value, Pj(), Agora),
            Condutor.Novo(b.EmpresaId.Value, DadosCondutor(pfB.Id), Agora));
        await outro.SaveChangesAsync();
    }

    [TestMethod]
    public async Task ChavesCompostasImpedemRelacionamentosCruzadosMesmoSemGuarda()
    {
        var a = await CriarEmpresaAsync();
        var b = await CriarEmpresaAsync();
        await using var bancoB = Banco(b);
        var grupoB = Grupo.Novo(b.EmpresaId!.Value, "Externo", Agora);
        var clienteB = Cliente.Novo(b.EmpresaId.Value, Pf(), Agora);
        bancoB.AddRange(grupoB, clienteB);
        await bancoB.SaveChangesAsync();
        await using var semGuarda = AmbienteIntegracao.CriarContexto(contexto: a);
        var empresaA = a.EmpresaId!.Value;
        foreach (var entidade in new object[]
        {
            Veiculo.Novo(empresaA, DadosVeiculo(grupoB.Id), Agora),
            Plano.Novo(empresaA, grupoB.Id, ModalidadePlano.Livre, 0m, null, null, null, Agora),
            Condutor.Novo(empresaA, DadosCondutor(clienteB.Id), Agora),
        })
        {
            semGuarda.Add(entidade);
            var erro = await Assert.ThrowsExactlyAsync<DbUpdateException>(() => semGuarda.SaveChangesAsync());
            Assert.AreEqual(547, ((SqlException)erro.InnerException!).Number);
            semGuarda.ChangeTracker.Clear();
        }
    }

    [TestMethod]
    public async Task ChecksEUnicidadeDeModalidadeSaoReforcadosNoSql()
    {
        var contexto = await CriarEmpresaAsync();
        await using var banco = Banco(contexto);
        var grupo = Grupo.Novo(contexto.EmpresaId!.Value, "Grupo", Agora);
        var plano = Plano.Novo(grupo.EmpresaId, grupo.Id, ModalidadePlano.Livre, 10m, null, null, null, Agora);
        var veiculo = Veiculo.Novo(grupo.EmpresaId, DadosVeiculo(grupo.Id), Agora);
        var taxa = TaxaServico.Nova(grupo.EmpresaId, "Taxa", 10m, ModalidadeTaxa.Fixa, Agora);
        banco.AddRange(grupo, plano, veiculo, taxa);
        await banco.SaveChangesAsync();
        banco.Add(Plano.Novo(grupo.EmpresaId, grupo.Id, ModalidadePlano.Livre, 20m, null, null, null, Agora));
        await DuplicidadeAsync(banco);
        banco.ChangeTracker.Clear();
        var erroPlano = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            banco.Database.ExecuteSqlInterpolatedAsync($"UPDATE Planos SET PrecoKm = 1 WHERE Id = {plano.Id}"));
        Assert.AreEqual(547, erroPlano.Number);
        var erroVeiculo = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            banco.Database.ExecuteSqlInterpolatedAsync($"UPDATE Veiculos SET CapacidadeLitros = 0 WHERE Id = {veiculo.Id}"));
        Assert.AreEqual(547, erroVeiculo.Number);
        var erroTaxa = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            banco.Database.ExecuteSqlInterpolatedAsync($"UPDATE TaxasServicos SET Preco = -1 WHERE Id = {taxa.Id}"));
        Assert.AreEqual(547, erroTaxa.Number);
    }

    [TestMethod]
    public async Task VigenciasEmpatadasRecebemOrdemSerialESaoImutaveisNaGuarda()
    {
        var contexto = await CriarEmpresaAsync();
        await using var banco = Banco(contexto);
        var unidade = new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
        var primeiro = await unidade.ExecutarAsync(token =>
        {
            var preco = PrecoCombustivel.Novo(contexto.EmpresaId!.Value, TipoCombustivel.Gasolina, 5m, Agora);
            banco.Add(preco);
            return Task.FromResult(preco);
        }, CancellationToken.None);
        var segundo = await unidade.ExecutarAsync(token =>
        {
            var preco = PrecoCombustivel.Novo(contexto.EmpresaId!.Value, TipoCombustivel.Gasolina, 6m, Agora);
            banco.Add(preco);
            return Task.FromResult(preco);
        }, CancellationToken.None);
        Assert.IsTrue(segundo.Ordem > primeiro.Ordem);
        Assert.AreEqual(primeiro.InicioVigenciaUtc, segundo.InicioVigenciaUtc);
        var vigente = await banco.PrecosCombustiveis.OrderByDescending(item => item.InicioVigenciaUtc)
            .ThenByDescending(item => item.Ordem).ThenByDescending(item => item.Id).FirstAsync();
        Assert.AreEqual(segundo.Id, vigente.Id);
        banco.Remove(vigente);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => banco.SaveChangesAsync());
    }

    [TestMethod]
    public async Task AuditoriaParticipaDoCommitEDoRollbackESemValoresPessoais()
    {
        var contexto = await CriarEmpresaAsync();
        await using var banco = Banco(contexto);
        var unidade = new UnidadeTrabalhoEmpresa(banco, contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
        var auditoria = new RegistroAuditoriaCadastro(banco, contexto, TimeProvider.System);
        var id = await unidade.ExecutarAsync(token =>
        {
            var grupo = Grupo.Novo(contexto.EmpresaId!.Value, "Grupo sintético", Agora);
            banco.Add(grupo);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.Grupo, grupo.Id, "nome");
            return Task.FromResult(grupo.Id);
        }, CancellationToken.None);
        Assert.IsTrue(await banco.Grupos.AnyAsync(item => item.Id == id));
        var registro = await banco.AuditoriasCadastro.SingleAsync();
        Assert.AreEqual("nome", registro.CamposAlterados);
        Assert.AreEqual(contexto.ContaId, registro.ContaId);
        Assert.AreEqual(id, registro.EntidadeId);
        Assert.AreEqual(contexto.CorrelacaoId, registro.CorrelacaoId);

        var descartado = Guid.Empty;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            unidade.ExecutarAsync<int>(async token =>
            {
                var grupo = Grupo.Novo(contexto.EmpresaId!.Value, "Descartado", Agora);
                descartado = grupo.Id;
                banco.Add(grupo);
                auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.Grupo, grupo.Id, "nome");
                await banco.SaveChangesAsync(token);
                throw new InvalidOperationException("Falha controlada.");
            }, CancellationToken.None));
        Assert.IsFalse(await banco.Grupos.AnyAsync(item => item.Id == descartado));
        Assert.IsFalse(await banco.AuditoriasCadastro.AnyAsync(item => item.EntidadeId == descartado));
        Assert.AreEqual(1, await banco.AuditoriasCadastro.CountAsync());
        Assert.ThrowsExactly<InvalidOperationException>(() => auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Grupo, id, "nome"));
    }

    private static async Task VerificarEmpresaAsync<T>(LocadoraDbContext banco, Guid? empresaId) where T : class, IEntidadeEmpresa
    {
        var entidades = await banco.Set<T>().ToListAsync();
        Assert.IsNotEmpty(entidades);
        Assert.IsTrue(entidades.All(item => item.EmpresaId == empresaId));
    }

    private static async Task DuplicidadeAsync(LocadoraDbContext banco)
    {
        var erro = await Assert.ThrowsExactlyAsync<DbUpdateException>(() => banco.SaveChangesAsync());
        Assert.IsTrue(erro.InnerException is SqlException { Number: 2601 or 2627 });
    }

    private static async Task PopularAsync(LocadoraDbContext banco, ContextoEmpresa contexto)
    {
        var empresaId = contexto.EmpresaId!.Value;
        var grupo = Grupo.Novo(empresaId, "Grupo sintético", Agora);
        var pf = Cliente.Novo(empresaId, Pf(), Agora);
        var pj = Cliente.Novo(empresaId, Pj(), Agora);
        banco.AddRange(grupo, pf, pj,
            Plano.Novo(empresaId, grupo.Id, ModalidadePlano.Livre, 100m, null, null, null, Agora),
            Veiculo.Novo(empresaId, DadosVeiculo(grupo.Id), Agora),
            Condutor.Novo(empresaId, DadosCondutor(pf.Id), Agora),
            VinculoCliente.Novo(pf, pj, Agora),
            TaxaServico.Nova(empresaId, "Taxa sintética", 5m, ModalidadeTaxa.Fixa, Agora),
            PrecoSeguro.Novo(empresaId, TipoSeguro.Cliente, 0m, Agora),
            PrecoCombustivel.Novo(empresaId, TipoCombustivel.Gasolina, 6m, Agora),
            AuditoriaCadastro.Registrar(empresaId, contexto.ContaId!.Value, Agora, AcaoCadastro.Criacao,
                TipoCadastro.Grupo, grupo.Id, contexto.CorrelacaoId!.Value, ["nome"]));
        await banco.SaveChangesAsync();
    }

    private static LocadoraDbContext Banco(ContextoEmpresa contexto) => AmbienteIntegracao.CriarContextoComGuarda(contexto);

    private static async Task<ContextoEmpresa> CriarEmpresaAsync()
    {
        var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true, CorrelacaoId = Guid.NewGuid() };
        await using var banco = Banco(contexto);
        var empresa = Empresa.Nova();
        var conta = Conta(empresa.Id);
        contexto.EmpresaId = empresa.Id;
        contexto.ContaId = conta.Id;
        banco.AddRange(empresa, conta);
        await banco.SaveChangesAsync();
        contexto.PermiteCriarEmpresa = false;
        return contexto;
    }

    private static UsuarioConta Conta(Guid empresaId)
    {
        var nome = "teste_" + Guid.NewGuid().ToString("N");
        return new UsuarioConta { Id = Guid.NewGuid(), EmpresaId = empresaId, UserName = nome, NormalizedUserName = nome.ToUpperInvariant() };
    }

    private static DadosVeiculo DadosVeiculo(Guid grupoId) =>
        new("ABC-1D23", "Marca sintética", "Azul", "Modelo sintético", TipoCombustivel.Gasolina, 48m, 2026, grupoId);

    private static DadosCliente Pf() => new(TipoCliente.PF, "Pessoa sintética", "123.000.000-32", null,
        "RG-SINTETICO", "CNH-SINTETICA", "000000000", new Endereco("Estado", "Cidade", "Bairro", "Rua", "S/N"), null);

    private static DadosCliente Pj() => new(TipoCliente.PJ, "PJ sintética", null, "12.ABC.345/01DE-35",
        null, null, "000000000", new Endereco("Estado", "Cidade", "Bairro", "Rua", "S/N"), null);

    private static DadosCondutor DadosCondutor(Guid clienteId) => new(clienteId, "Condutor sintético",
        "condutor@example.invalid", "123.000.000-32", "CNH-SINTETICA", new DateOnly(2028, 1, 1), "000000000");
}
