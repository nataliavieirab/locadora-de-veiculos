using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class IntegridadeLocacoesTests
{
    [TestMethod]
    public async Task GuardaRejeitaUpdateDeleteEAlteracaoComplexaDosRegistrosConfirmados()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var id = await ContratarAsync(cenario);
        var banco = cenario.Banco;
        for (var indice = 0; indice < 6; indice++)
        {
            foreach (var estado in new[] { EntityState.Modified, EntityState.Deleted })
            {
                banco.ChangeTracker.Clear();
                var aluguel = await CenarioLocacoesSql.Contratos(banco).SingleAsync(a => a.Id == id);
                var comando = await banco.ComandosIdempotentes.SingleAsync();
                object[] imutaveis = [aluguel.RevisaoAtual, aluguel.RevisaoAtual.Itens.First(),
                    aluguel.RevisaoAtual.Estimativa, aluguel.RevisaoAtual.Estimativa.Componentes.First(),
                    comando, await banco.AuditoriasCadastro.SingleAsync()];
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                {
                    banco.Entry(imutaveis[indice]).State = estado;
                    await banco.SaveChangesAsync();
                });
            }
        }
        banco.ChangeTracker.Clear();
        var revisao = await banco.RevisoesContrato.SingleAsync();
        banco.Entry(revisao).ComplexProperty(r => r.Cliente).Property(c => c.Nome).CurrentValue = "Tentativa indevida";
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => banco.SaveChangesAsync());
        banco.ChangeTracker.Clear();
        Assert.AreEqual("Cliente sintético", (await banco.RevisoesContrato.SingleAsync()).Cliente.Nome);
    }

    [TestMethod]
    public async Task IsolaTodaArvoreEResultadosEntreEmpresasESemContexto()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        await ContratarAsync(a);
        await ContratarAsync(b);
        foreach (var tipo in new[] { typeof(Aluguel), typeof(RevisaoContrato), typeof(ItemContratado),
            typeof(DemonstrativoEstimado), typeof(ComponenteEstimativa), typeof(ComandoIdempotente) })
        {
            var entidade = a.Banco.Model.FindEntityType(tipo)!;
            Assert.IsTrue(entidade.GetDeclaredQueryFilters().Any());
            Assert.IsTrue(entidade.GetForeignKeys().All(fk => fk.DeleteBehavior == DeleteBehavior.NoAction));
        }
        Assert.IsTrue((await a.Banco.RevisoesContrato.ToListAsync()).All(r => r.EmpresaId == a.Contexto.EmpresaId));
        Assert.IsTrue((await a.Banco.ItensContratados.ToListAsync()).All(r => r.EmpresaId == a.Contexto.EmpresaId));
        Assert.IsTrue((await a.Banco.ComponentesEstimativa.ToListAsync()).All(r => r.EmpresaId == a.Contexto.EmpresaId));
        Assert.AreEqual(1, await a.Banco.Alugueis.CountAsync());
        Assert.AreEqual(1, await a.Banco.DemonstrativosEstimados.CountAsync());
        Assert.AreEqual(1, await a.Banco.ComandosIdempotentes.CountAsync());
        await using var semContexto = AmbienteIntegracao.CriarContexto();
        Assert.AreEqual(0, await semContexto.Alugueis.CountAsync());
        Assert.AreEqual(0, await semContexto.RevisoesContrato.CountAsync());
        Assert.AreEqual(0, await semContexto.ItensContratados.CountAsync());
        Assert.AreEqual(0, await semContexto.DemonstrativosEstimados.CountAsync());
        Assert.AreEqual(0, await semContexto.ComponentesEstimativa.CountAsync());
        Assert.AreEqual(0, await semContexto.ComandosIdempotentes.CountAsync());
        a.Banco.ChangeTracker.Clear();
        var externo = await b.Banco.RevisoesContrato.AsNoTracking().SingleAsync();
        a.Banco.Add(externo);
        await Assert.ThrowsExactlyAsync<EmpresaDivergenteException>(() => a.Banco.SaveChangesAsync());
    }

    [TestMethod]
    public async Task FksNegamReferenciasCruzadasInclusiveFilhosEAtorSemDependerDaGuarda()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var idA = await ContratarAsync(a);
        var idB = await ContratarAsync(b);
        var aluguelA = await CenarioLocacoesSql.Contratos(a.Banco).SingleAsync();
        var aluguelB = await CenarioLocacoesSql.Contratos(b.Banco).SingleAsync();
        foreach (var (tabela, coluna, id, externo) in new (string, string, Guid, Guid)[]
        {
            ("Alugueis", "ClienteId", idA, b.Dados.Cliente.Id),
            ("Alugueis", "CondutorId", idA, b.Dados.Condutor.Id),
            ("Alugueis", "VeiculoId", idA, b.Dados.Veiculo.Id),
            ("Alugueis", "PlanoId", idA, b.Dados.Plano.Id),
            ("RevisoesContrato", "AluguelId", aluguelA.RevisaoAtual.Id, idB),
            ("ItensContratados", "RevisaoId", aluguelA.RevisaoAtual.Itens.First().Id, aluguelB.RevisaoAtual.Id),
            ("DemonstrativosEstimados", "RevisaoId", aluguelA.RevisaoAtual.Estimativa.Id, aluguelB.RevisaoAtual.Id),
            ("ComponentesEstimativa", "DemonstrativoId", aluguelA.RevisaoAtual.Estimativa.Componentes.First().Id, aluguelB.RevisaoAtual.Estimativa.Id),
            ("ComandosIdempotentes", "ContaId", (await a.Banco.ComandosIdempotentes.SingleAsync()).Id, b.Contexto.ContaId!.Value),
            ("ComandosIdempotentes", "RevisaoId", (await a.Banco.ComandosIdempotentes.SingleAsync()).Id, aluguelB.RevisaoAtual.Id),
        })
        {
            // Tabela/coluna vêm apenas da lista literal acima; valores são parâmetros.
            var sql = $"UPDATE [{tabela}] SET [{coluna}] = @externo WHERE Id = @id";
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() => a.Banco.Database.ExecuteSqlRawAsync(sql,
                new SqlParameter("@externo", externo), new SqlParameter("@id", id)));
            Assert.AreEqual(547, erro.Number);
        }
    }

    [TestMethod]
    public async Task IndicesNegamSegundoAbertoERevisaoOuItemDuplicados()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var id = await ContratarAsync(cenario);
        var banco = cenario.Banco;
        var segundo = Aluguel.Criar(cenario.Dados.Cliente.EmpresaId, cenario.Dados, CenarioLocacoesSql.Agora);
        banco.Add(segundo);
        var erro = await Assert.ThrowsExactlyAsync<DbUpdateException>(() => banco.SaveChangesAsync());
        Assert.IsTrue(((SqlException)erro.InnerException!).Number is 2601 or 2627);
        banco.ChangeTracker.Clear();
        foreach (var tabela in new[] { "RevisoesContrato", "ItensContratados" })
        {
            // Identificadores abaixo são nomes fixos de tabelas de teste, não entrada externa.
            var colunas = await banco.Database.SqlQuery<string>(
                $"SELECT QUOTENAME(name) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID({tabela}) AND name <> N'Id' ORDER BY column_id").ToListAsync();
            var lista = string.Join(",", colunas);
            var sql = $"INSERT INTO [{tabela}] ([Id],{lista}) SELECT TOP(1) NEWID(),{lista} FROM [{tabela}] WHERE EmpresaId = @empresa";
            var duplicado = await Assert.ThrowsExactlyAsync<SqlException>(() => banco.Database.ExecuteSqlRawAsync(
                sql, new SqlParameter("@empresa", cenario.Contexto.EmpresaId!.Value)));
            Assert.IsTrue(duplicado.Number is 2601 or 2627);
        }
        Assert.AreEqual(id, (await banco.Alugueis.SingleAsync()).Id);
    }

    [TestMethod]
    public async Task RaizTemRowversionInclusiveSomenteItemESaidaNaoPodeSerAlterada()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        await ContratarAsync(cenario);
        var banco = cenario.Banco;
        banco.ChangeTracker.Clear();
        var aluguel = await CenarioLocacoesSql.Contratos(banco).SingleAsync();
        var anterior = aluguel.RowVersion.ToArray();
        aluguel.Editar(cenario.Dados with { Taxas = [] }, CenarioLocacoesSql.Agora.AddMinutes(1));
        await banco.SaveChangesAsync();
        Assert.IsFalse(anterior.SequenceEqual(aluguel.RowVersion));
        Assert.AreEqual(2, await banco.RevisoesContrato.CountAsync());
        Assert.AreEqual(254m, (await banco.RevisoesContrato.Include(r => r.Estimativa).SingleAsync(r => r.Numero == 1)).Estimativa.TotalConhecido);
        banco.Entry(aluguel).Property(a => a.SaidaUtc).CurrentValue = aluguel.SaidaUtc.AddMinutes(1);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => banco.SaveChangesAsync());
    }

    [TestMethod]
    public async Task FalhaAposSaveAntesDoCommitReverteArvoreAuditoriaEResultado()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var banco = cenario.Banco;
        var unidade = new UnidadeTrabalhoEmpresa(banco, cenario.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => unidade.ExecutarAsync<int>(async token =>
        {
            await InserirContratoAsync(cenario);
            await banco.SaveChangesAsync(token);
            throw new InvalidOperationException("Falha sintética antes do commit.");
        }, CancellationToken.None));
        Assert.AreEqual(0, await banco.Alugueis.CountAsync());
        Assert.AreEqual(0, await banco.RevisoesContrato.CountAsync());
        Assert.AreEqual(0, await banco.ItensContratados.CountAsync());
        Assert.AreEqual(0, await banco.DemonstrativosEstimados.CountAsync());
        Assert.AreEqual(0, await banco.ComponentesEstimativa.CountAsync());
        Assert.AreEqual(0, await banco.AuditoriasCadastro.CountAsync());
        Assert.AreEqual(0, await banco.ComandosIdempotentes.CountAsync());
    }

    [TestMethod]
    public async Task AuditoriaAceitaSoNomesTecnicosESemRenumerarCadastros()
    {
        Assert.AreEqual(8, (int)TipoCadastro.PrecoCombustivel);
        Assert.AreEqual(9, (int)TipoCadastro.Aluguel);
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        await ContratarAsync(cenario);
        var registro = await cenario.Banco.AuditoriasCadastro.SingleAsync();
        Assert.AreEqual(TipoCadastro.Aluguel, registro.Entidade);
        Assert.AreEqual("clienteId,condutorId,hodometroInicial,itens,planoId,retornoPrevistoUtc,revisao,veiculoId", registro.CamposAlterados);
        Assert.IsFalse(registro.CamposAlterados.Contains(cenario.Dados.Cliente.Cpf!, StringComparison.Ordinal));
        Assert.ThrowsExactly<ArgumentException>(() => AuditoriaCadastro.Registrar(registro.EmpresaId, registro.ContaId,
            CenarioLocacoesSql.Agora, AcaoCadastro.Alteracao, TipoCadastro.Aluguel, registro.EntidadeId, Guid.NewGuid(),
            [cenario.Dados.Cliente.Cpf!]));
    }

    private static async Task<Guid> ContratarAsync(CenarioLocacoesSql cenario)
    {
        var unidade = new UnidadeTrabalhoEmpresa(cenario.Banco, cenario.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);
        return await unidade.ExecutarAsync(_ => InserirContratoAsync(cenario), CancellationToken.None);
    }

    private static async Task<Guid> InserirContratoAsync(CenarioLocacoesSql cenario)
    {
        var aluguel = Aluguel.Criar(cenario.Dados.Cliente.EmpresaId, cenario.Dados, CenarioLocacoesSql.Agora);
        cenario.Banco.Add(aluguel);
        cenario.Banco.Add(AuditoriaCadastro.Registrar(aluguel.EmpresaId, cenario.Contexto.ContaId!.Value,
            CenarioLocacoesSql.Agora, AcaoCadastro.Criacao, TipoCadastro.Aluguel, aluguel.Id,
            cenario.Contexto.CorrelacaoId!.Value, ["clienteId", "condutorId", "veiculoId", "planoId", "retornoPrevistoUtc", "hodometroInicial", "itens", "revisao"]));
        await cenario.Banco.SaveChangesAsync();
        cenario.Banco.Add(ComandoIdempotente.RegistrarCriacao(aluguel.EmpresaId, cenario.Contexto.ContaId.Value,
            Guid.NewGuid(), new string('A', 64), aluguel.RevisaoAtual.Id, aluguel.RowVersion, CenarioLocacoesSql.Agora));
        return aluguel.Id;
    }
}
