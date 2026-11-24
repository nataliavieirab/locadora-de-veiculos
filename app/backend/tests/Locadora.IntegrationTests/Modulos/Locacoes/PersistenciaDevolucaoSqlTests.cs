using System.Text.Json;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class PersistenciaDevolucaoSqlTests
{
    [TestMethod]
    public async Task SchemaNovoTemPrecisaoFksSemCascataUnicidadeEFiltrosNomeados()
    {
        await using var banco = AmbienteIntegracao.CriarContexto();
        Assert.IsFalse(banco.Database.HasPendingModelChanges());
        foreach (var tabela in new[] { "DemonstrativosFinais", "ItensDevolucao", "ComponentesFinal" })
        {
            Assert.AreEqual(1, await banco.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = {tabela}").SingleAsync());
            Assert.AreEqual(0, await banco.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID({tabela}) AND delete_referential_action <> 0").SingleAsync());
        }
        foreach (var (tabela, coluna, esperado) in new[]
        {
            ("DemonstrativosFinais", "TotalFinal", 1902), ("DemonstrativosFinais", "PrecoCombustivel", 1906),
            ("DemonstrativosFinais", "LitrosRestantes", 1203), ("DemonstrativosFinais", "TermoKmBruto", 2809),
            ("ComponentesFinal", "Quantidade", 1203), ("ComponentesFinal", "ValorBruto", 2809),
        })
            Assert.AreEqual(esperado, await banco.Database.SqlQuery<int>(
                $"SELECT CAST(precision AS int) * 100 + CAST(scale AS int) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID({tabela}) AND name = {coluna}").SingleAsync());
        var filtros = banco.Model.FindEntityType(typeof(Aluguel))!.GetDeclaredQueryFilters().Select(f => f.Key).ToArray();
        CollectionAssert.AreEquivalent(new[] { "Empresa", "ExclusaoLogicaAluguel" }, filtros);
        Assert.AreEqual(1, await banco.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID(N'DemonstrativosFinais') AND name = N'IX_DemonstrativosFinais_EmpresaId_AluguelId' AND is_unique = 1").SingleAsync());
    }

    [TestMethod]
    public async Task PersisteFinalComponentesItensEVersaoSemAlterarRevisao()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        var original = JsonSerializer.Serialize((await ContratosComFinal(cenario.Banco).AsNoTracking().SingleAsync()).RevisaoAtual);
        var dados = await PrepararDadosAsync(cenario);
        var versao = aluguel.RowVersion.ToArray();
        await FecharAsync(cenario, aluguel.Id, dados);
        cenario.Banco.ChangeTracker.Clear();
        var lido = await ContratosComFinal(cenario.Banco).SingleAsync();
        Assert.AreEqual(EstadoAluguel.Concluido, lido.Estado);
        Assert.AreEqual(original, JsonSerializer.Serialize(lido.RevisaoAtual));
        Assert.IsFalse(versao.SequenceEqual(lido.RowVersion));
        Assert.AreEqual(334m, lido.Devolucao!.TotalFinal);
        Assert.HasCount(1, lido.Devolucao.Itens);
        Assert.HasCount(7, lido.Devolucao.Componentes);
        Assert.AreEqual(10m, lido.Devolucao.Componentes.Single(c => c.Categoria == CategoriaComponenteFinal.Combustivel).Quantidade);
        CollectionAssert.AreEqual(dados.TaxasAdicionais.Single().RowVersion, lido.Devolucao.Itens.Single().VersaoTaxa);
        Assert.AreEqual(1, await cenario.Banco.AuditoriasCadastro.CountAsync(a => a.Acao == AcaoCadastro.Devolucao));
        Assert.AreEqual(1, await cenario.Banco.ComandosIdempotentes.CountAsync(c => c.Operacao == "devolver-aluguel"));
    }

    [TestMethod]
    public async Task GuardaRejeitaUpdateDeleteComplexoEReaberturaDoFinal()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        await FecharAsync(cenario, aluguel.Id, await PrepararDadosAsync(cenario));
        var banco = cenario.Banco;
        for (var indice = 0; indice < 3; indice++)
        {
            foreach (var estado in new[] { EntityState.Modified, EntityState.Deleted })
            {
                banco.ChangeTracker.Clear();
                var lido = await ContratosComFinal(banco).SingleAsync();
                object[] imutaveis = [lido.Devolucao!, lido.Devolucao!.Itens.Single(), lido.Devolucao.Componentes.First()];
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                {
                    banco.Entry(imutaveis[indice]).State = estado;
                    await banco.SaveChangesAsync();
                });
            }
        }
        banco.ChangeTracker.Clear();
        var final = await banco.DemonstrativosFinais.SingleAsync();
        banco.Entry(final).ComplexProperty(f => f.Plano).Property(p => p.PrecoDiario).CurrentValue = 999m;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => banco.SaveChangesAsync());
        banco.ChangeTracker.Clear();
        var concluido = await ContratosComFinal(banco).SingleAsync();
        banco.Entry(concluido).Property(a => a.Estado).CurrentValue = EstadoAluguel.EmAberto;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => banco.SaveChangesAsync());
        banco.ChangeTracker.Clear();
        Assert.AreEqual(334m, (await banco.DemonstrativosFinais.SingleAsync()).TotalFinal);
    }

    [TestMethod]
    public async Task GuardaExigeTransacaoAuditoriaEArvoreCoerenteNaConclusao()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(cenario);
        var dados = await PrepararDadosAsync(cenario);
        var atualizado = await ContratosComFinal(cenario.Banco).SingleAsync();
        atualizado.Concluir(dados, CenarioLocacoesSql.Agora.AddDays(2));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => cenario.Banco.SaveChangesAsync());
        cenario.Banco.ChangeTracker.Clear();
        var unidade = Unidade(cenario);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => unidade.ExecutarAsync<int>(async token =>
        {
            var lido = await ContratosComFinal(cenario.Banco).SingleAsync(token);
            lido.Concluir(dados, CenarioLocacoesSql.Agora.AddDays(2));
            await cenario.Banco.SaveChangesAsync(token);
            return 0;
        }, CancellationToken.None));
        cenario.Banco.Add(Auditoria(cenario, aluguel.Id, AcaoCadastro.Devolucao));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => cenario.Banco.SaveChangesAsync());
        cenario.Banco.ChangeTracker.Clear();
        Assert.AreEqual(EstadoAluguel.EmAberto, (await cenario.Banco.Alugueis.SingleAsync()).Estado);
        Assert.AreEqual(0, await cenario.Banco.DemonstrativosFinais.CountAsync());
    }

    [TestMethod]
    public async Task FksNegamEmpresaERevisaoDeOutroAluguelEIndicesNegamDuplicacao()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var aluguelA = await CriarAbertoAsync(a);
        var aluguelB = await CriarAbertoAsync(b);
        await FecharAsync(a, aluguelA.Id, await PrepararDadosAsync(a));
        await FecharAsync(b, aluguelB.Id, await PrepararDadosAsync(b));
        var finalA = await a.Banco.DemonstrativosFinais.Include(f => f.Itens).Include(f => f.Componentes).SingleAsync();
        var finalB = await b.Banco.DemonstrativosFinais.SingleAsync();
        var outro = await CriarAbertoAsync(a);
        foreach (var (tabela, coluna, id, referencia) in new[]
        {
            ("DemonstrativosFinais", "RevisaoId", finalA.Id, outro.RevisaoAtual.Id),
            ("DemonstrativosFinais", "AluguelId", finalA.Id, aluguelB.Id),
            ("DemonstrativosFinais", "PrecoCombustivelId", finalA.Id, finalB.PrecoCombustivelId),
            ("ItensDevolucao", "DemonstrativoFinalId", finalA.Itens.Single().Id, finalB.Id),
            ("ItensDevolucao", "TaxaServicoId", finalA.Itens.Single().Id, b.Dados.Taxas.Single().Id),
            ("ComponentesFinal", "DemonstrativoFinalId", finalA.Componentes.First().Id, finalB.Id),
        })
        {
            // Identificadores vêm exclusivamente da lista literal do teste; valores são parâmetros.
            var sql = $"UPDATE [{tabela}] SET [{coluna}] = @referencia WHERE Id = @id";
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() => a.Banco.Database.ExecuteSqlRawAsync(
                sql,
                new SqlParameter("@referencia", referencia), new SqlParameter("@id", id)));
            Assert.AreEqual(547, erro.Number);
        }
        foreach (var tabela in new[] { "DemonstrativosFinais", "ItensDevolucao", "ComponentesFinal" })
        {
            var colunas = await a.Banco.Database.SqlQuery<string>(
                $"SELECT QUOTENAME(name) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID({tabela}) AND name <> N'Id' ORDER BY column_id").ToListAsync();
            var lista = string.Join(",", colunas);
            var sql = $"INSERT INTO [{tabela}] ([Id],{lista}) SELECT TOP(1) NEWID(),{lista} FROM [{tabela}] WHERE EmpresaId = @empresa";
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() => a.Banco.Database.ExecuteSqlRawAsync(
                sql,
                new SqlParameter("@empresa", a.Contexto.EmpresaId!.Value)));
            Assert.IsTrue(erro.Number is 2601 or 2627);
        }
    }

    [TestMethod]
    public async Task FilhosNegamEmpresaExternaESemContextoEChecksNegamValoresInvalidos()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        var aluguel = await CriarAbertoAsync(a);
        var erroAberto = await Assert.ThrowsExactlyAsync<SqlException>(() => a.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Alugueis SET ExcluidoEmUtc = {CenarioLocacoesSql.Agora} WHERE Id = {aluguel.Id}"));
        Assert.AreEqual(547, erroAberto.Number);
        await FecharAsync(a, aluguel.Id, await PrepararDadosAsync(a));
        await using var b = await CenarioLocacoesSql.CriarAsync();
        await using var semContexto = AmbienteIntegracao.CriarContexto();
        foreach (var banco in new[] { b.Banco, semContexto })
        {
            Assert.AreEqual(0, await banco.DemonstrativosFinais.CountAsync());
            Assert.AreEqual(0, await banco.ItensDevolucao.CountAsync());
            Assert.AreEqual(0, await banco.ComponentesFinal.CountAsync());
        }
        var externo = await a.Banco.DemonstrativosFinais.AsNoTracking().SingleAsync();
        b.Banco.Add(externo);
        await Assert.ThrowsExactlyAsync<EmpresaDivergenteException>(() => b.Banco.SaveChangesAsync());
        foreach (var atribuicao in new[]
        {
            "Caucao = 0", "RetornoRealUtc = DATEADD(second, -1, SaidaUtc)", "HodometroFinal = HodometroInicial - 1",
            "LitrosRestantes = -1", "LitrosRestantes = CapacidadeLitros + 1", "TotalFinal = -1", "Diarias = 0",
        })
        {
            // Atribuições exclusivamente literais acima, sem entrada HTTP ou dado externo.
            var sql = $"UPDATE DemonstrativosFinais SET {atribuicao} WHERE Id = @id";
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() => a.Banco.Database.ExecuteSqlRawAsync(
                sql, new SqlParameter("@id", externo.Id)));
            Assert.AreEqual(547, erro.Number);
        }
    }

    [TestMethod]
    public async Task EvolucaoDaF3PreservaContratoChaveEVersaoSemBackfillERollbackProtegeFinal()
    {
        var conexao = await AmbienteIntegracao.CriarCatalogoVazioAsync();
        try
        {
            await using var inicial = AmbienteIntegracao.CriarContexto(conexao);
            var assembly = inicial.GetService<IMigrationsAssembly>();
            var idF3 = assembly.Migrations.Keys.Single(m => m.EndsWith("_ContratacaoFase3", StringComparison.Ordinal));
            await inicial.GetService<IMigrator>().MigrateAsync(idF3);
            await using var origem = await CenarioLocacoesSql.CriarAsync();
            var aluguel = await CriarAbertoAsync(origem);
            var comandoOriginal = await origem.Banco.ComandosIdempotentes.SingleAsync();
            var catalogoOrigem = new SqlConnectionStringBuilder(origem.Banco.Database.GetConnectionString()).InitialCatalog;
            Assert.IsTrue(catalogoOrigem.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'));
            // Copia somente as colunas do schema F3 e a empresa sintética selecionada, em ordem de FKs.
            // Não usa TargetModel de migration como modelo runtime: seus tipos são de design, não os CLR da aplicação.
            foreach (var tabela in new[] { "Empresas", "AspNetUsers", "Grupos", "Planos", "Veiculos", "Clientes",
                "Condutores", "TaxasServicos", "PrecosSeguros", "Alugueis", "RevisoesContrato", "ItensContratados",
                "DemonstrativosEstimados", "ComponentesEstimativa", "ComandosIdempotentes" })
            {
                var colunas = await inicial.Database.SqlQuery<string>(
                    $"SELECT QUOTENAME(name) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID({tabela}) AND system_type_id <> 189 ORDER BY column_id").ToListAsync();
                var lista = string.Join(",", colunas);
                var campoEmpresa = tabela == "Empresas" ? "Id" : "EmpresaId";
                var sql = $"INSERT INTO [{tabela}] ({lista}) SELECT {lista} FROM [{catalogoOrigem}].dbo.[{tabela}] WHERE [{campoEmpresa}] = @empresa";
                await inicial.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@empresa", origem.Contexto.EmpresaId!.Value));
            }
            var versao = await inicial.Database.SqlQuery<byte[]>($"SELECT RowVersion AS [Value] FROM Alugueis WHERE Id = {aluguel.Id}").SingleAsync();
            var chave = comandoOriginal.Chave;
            var revisaoId = aluguel.RevisaoAtual.Id;
            await using var cenario = new CenarioLocacoesSql(origem.Contexto,
                AmbienteIntegracao.CriarContextoComGuarda(origem.Contexto, conexao), origem.Dados);
            await cenario.Banco.Database.MigrateAsync();
            var lido = await ContratosComFinal(cenario.Banco).SingleAsync();
            Assert.AreEqual(revisaoId, lido.RevisaoAtual.Id);
            CollectionAssert.AreEqual(versao, lido.RowVersion);
            Assert.AreEqual(EstadoAluguel.EmAberto, lido.Estado);
            Assert.IsNull(lido.Devolucao);
            Assert.IsNull(lido.ExcluidoEmUtc);
            Assert.AreEqual(0, await cenario.Banco.DemonstrativosFinais.CountAsync());
            var comando = await cenario.Banco.ComandosIdempotentes.SingleAsync();
            Assert.AreEqual(chave, comando.Chave);
            Assert.IsNull(comando.DemonstrativoFinalId);
            CollectionAssert.AreEqual(comandoOriginal.VersaoOriginal, comando.VersaoOriginal);
            await FecharAsync(cenario, lido.Id, await PrepararDadosAsync(cenario));
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() => cenario.Banco.GetService<IMigrator>().MigrateAsync(idF3));
            Assert.AreEqual(51004, erro.Number);
            Assert.AreEqual(1, await cenario.Banco.DemonstrativosFinais.CountAsync());
        }
        finally
        {
            await AmbienteIntegracao.RemoverCatalogoAsync(conexao);
        }
    }

    internal static IQueryable<Aluguel> ContratosComFinal(LocadoraDbContext banco)
        => CenarioLocacoesSql.Contratos(banco).Include(a => a.Devolucao).ThenInclude(f => f!.Itens)
            .Include(a => a.Devolucao).ThenInclude(f => f!.Componentes).AsSplitQuery();

    internal static UnidadeTrabalhoEmpresa Unidade(CenarioLocacoesSql cenario)
        => new(cenario.Banco, cenario.Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance);

    internal static async Task<Aluguel> CriarAbertoAsync(CenarioLocacoesSql cenario)
        => await Unidade(cenario).ExecutarAsync(async token =>
        {
            var aluguel = Aluguel.Criar(cenario.Contexto.EmpresaId!.Value, cenario.Dados, CenarioLocacoesSql.Agora);
            cenario.Banco.Add(aluguel);
            await cenario.Banco.SaveChangesAsync(token);
            cenario.Banco.Add(ComandoIdempotente.RegistrarCriacao(aluguel.EmpresaId, cenario.Contexto.ContaId!.Value,
                Guid.NewGuid(), new string('A', 64), aluguel.RevisaoAtual.Id, aluguel.RowVersion, CenarioLocacoesSql.Agora));
            return aluguel;
        }, CancellationToken.None);

    internal static async Task<DadosDevolucao> PrepararDadosAsync(CenarioLocacoesSql cenario)
        => await Unidade(cenario).ExecutarAsync(_ =>
        {
            var preco = PrecoCombustivel.Novo(cenario.Contexto.EmpresaId!.Value, TipoCombustivel.Gasolina, 6m, CenarioLocacoesSql.Agora);
            var taxa = TaxaServico.Nova(cenario.Contexto.EmpresaId.Value, "Taxa no retorno", 10m, ModalidadeTaxa.Diaria, CenarioLocacoesSql.Agora);
            cenario.Banco.AddRange(preco, taxa);
            return Task.FromResult(new DadosDevolucao(CenarioLocacoesSql.Agora.AddDays(2), 1150, 40m, preco, [taxa]));
        }, CancellationToken.None);

    internal static async Task<DemonstrativoFinal> FecharAsync(CenarioLocacoesSql cenario, Guid id,
        DadosDevolucao dados, Guid? chave = null)
        => await Unidade(cenario).ExecutarAsync(async token =>
        {
            var aluguel = await ContratosComFinal(cenario.Banco).SingleAsync(a => a.Id == id, token);
            var final = aluguel.Concluir(dados, CenarioLocacoesSql.Agora.AddDays(2));
            cenario.Banco.Add(Auditoria(cenario, id, AcaoCadastro.Devolucao));
            await cenario.Banco.SaveChangesAsync(token);
            cenario.Banco.Add(ComandoIdempotente.RegistrarDevolucao(aluguel.EmpresaId, cenario.Contexto.ContaId!.Value,
                chave ?? Guid.NewGuid(), new string('B', 64), final.RevisaoId, final.Id,
                aluguel.RowVersion, CenarioLocacoesSql.Agora.AddDays(2)));
            return final;
        }, CancellationToken.None);

    internal static AuditoriaCadastro Auditoria(CenarioLocacoesSql cenario, Guid id, AcaoCadastro acao)
        => AuditoriaCadastro.Registrar(cenario.Contexto.EmpresaId!.Value, cenario.Contexto.ContaId!.Value,
            CenarioLocacoesSql.Agora.AddDays(2), acao, TipoCadastro.Aluguel, id, cenario.Contexto.CorrelacaoId!.Value,
            ["retornoRealUtc", "hodometroFinal", "litrosRestantes", "demonstrativoFinal", "estado"]);
}
