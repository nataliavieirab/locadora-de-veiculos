using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

[TestClass]
public sealed class SchemaLocacoesTests
{
    [TestMethod]
    public async Task UpgradeDaF2PreservaDadosEVersoesEPermiteContratoCompleto()
    {
        var conexao = await AmbienteIntegracao.CriarCatalogoVazioAsync();
        try
        {
            await using (var inicial = AmbienteIntegracao.CriarContexto(conexao))
            {
                var migrations = inicial.Database.GetMigrations().ToArray();
                var f2 = migrations[Array.FindIndex(migrations, m => m.EndsWith("_ContratacaoFase3", StringComparison.Ordinal)) - 1];
                await inicial.GetService<IMigrator>().MigrateAsync(f2);
            }
            await using var cenario = await CenarioLocacoesSql.CriarAsync(conexao);
            var dados = cenario.Dados;
            var versoes = new[] { dados.Cliente.RowVersion, dados.Condutor.RowVersion, dados.Veiculo.RowVersion,
                dados.Grupo.RowVersion, dados.Plano.RowVersion, dados.Taxas.Single().RowVersion }.Select(v => v.ToArray()).ToArray();
            var banco = cenario.Banco;
            await banco.Database.MigrateAsync();
            banco.ChangeTracker.Clear();
            Assert.IsFalse(banco.Database.HasPendingModelChanges());
            CollectionAssert.AreEqual(versoes[0], (await banco.Clientes.SingleAsync()).RowVersion);
            CollectionAssert.AreEqual(versoes[1], (await banco.Condutores.SingleAsync()).RowVersion);
            CollectionAssert.AreEqual(versoes[2], (await banco.Veiculos.SingleAsync()).RowVersion);
            CollectionAssert.AreEqual(versoes[3], (await banco.Grupos.SingleAsync()).RowVersion);
            CollectionAssert.AreEqual(versoes[4], (await banco.Planos.SingleAsync()).RowVersion);
            CollectionAssert.AreEqual(versoes[5], (await banco.TaxasServicos.SingleAsync()).RowVersion);
            Assert.AreEqual(2, await banco.PrecosSeguros.CountAsync());
            Assert.AreEqual("Cliente sintético", (await banco.Clientes.SingleAsync()).Nome);
            Assert.AreEqual(100m, (await banco.Planos.SingleAsync()).PrecoDiario);
            banco.ChangeTracker.Clear();
            var aluguel = Aluguel.Criar(dados.Cliente.EmpresaId, dados, CenarioLocacoesSql.Agora);
            banco.Add(aluguel);
            await banco.SaveChangesAsync();
            var comando = ComandoIdempotente.RegistrarCriacao(aluguel.EmpresaId, cenario.Contexto.ContaId!.Value,
                Guid.NewGuid(), new string('A', 64), aluguel.RevisaoAtual.Id, aluguel.RowVersion, CenarioLocacoesSql.Agora);
            banco.Add(comando);
            await banco.SaveChangesAsync();
            banco.ChangeTracker.Clear();
            var lido = await CenarioLocacoesSql.Contratos(banco).SingleAsync();
            Assert.AreEqual(8, lido.RowVersion.Length);
            Assert.AreEqual(254m, lido.RevisaoAtual.Estimativa.TotalConhecido);
            Assert.AreEqual(3, lido.RevisaoAtual.Itens.Count);
            Assert.AreEqual(4, lido.RevisaoAtual.Estimativa.Componentes.Count);
            Assert.AreEqual(dados.Cliente.Cpf, lido.RevisaoAtual.Cliente.Cpf);
            Assert.AreEqual(dados.Cliente.Endereco.Rua, lido.RevisaoAtual.Cliente.Endereco.Rua);
            Assert.AreEqual(lido.RevisaoAtual.Id, (await banco.ComandosIdempotentes.SingleAsync()).RevisaoId);
        }
        finally
        {
            await AmbienteIntegracao.RemoverCatalogoAsync(conexao);
        }
    }

    [TestMethod]
    public async Task SchemaRealTemPrecisaoChecksFksSemCascataEIndiceFiltrado()
    {
        await using var banco = AmbienteIntegracao.CriarContexto();
        var tabelas = new[] { "Alugueis", "RevisoesContrato", "ItensContratados", "DemonstrativosEstimados", "ComponentesEstimativa", "ComandosIdempotentes" };
        foreach (var tabela in tabelas)
        {
            var qtd = await banco.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = {tabela}").SingleAsync();
            Assert.AreEqual(1, qtd);
            var cascatas = await banco.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID({tabela}) AND delete_referential_action <> 0").SingleAsync();
            Assert.AreEqual(0, cascatas);
        }
        var indice = await banco.Database.SqlQuery<string>(
            $"SELECT filter_definition AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID(N'Alugueis') AND name = N'IX_Alugueis_EmpresaId_VeiculoId' AND is_unique = 1").SingleAsync();
        Assert.IsTrue(indice.Contains("[Estado]=(0)", StringComparison.Ordinal));
        var bruto = await banco.Database.SqlQuery<int>(
            $"SELECT CAST(precision AS int) * 100 + CAST(scale AS int) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID(N'ComponentesEstimativa') AND name = N'ValorBruto'").SingleAsync();
        Assert.AreEqual(2809, bruto);
        var total = await banco.Database.SqlQuery<int>(
            $"SELECT CAST(precision AS int) * 100 + CAST(scale AS int) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID(N'DemonstrativosEstimados') AND name = N'TotalConhecido'").SingleAsync();
        Assert.AreEqual(1902, total);
    }

    [TestMethod]
    public async Task ChecksSqlNegamCaucaoPrazoEValoresInvalidos()
    {
        await using var cenario = await CenarioLocacoesSql.CriarAsync();
        var banco = cenario.Banco;
        var aluguel = Aluguel.Criar(cenario.Dados.Cliente.EmpresaId, cenario.Dados, CenarioLocacoesSql.Agora);
        banco.Add(aluguel);
        await banco.SaveChangesAsync();
        foreach (var sql in new[]
        {
            "UPDATE Alugueis SET RetornoPrevistoUtc = SaidaUtc WHERE Id = @id",
            "UPDATE Alugueis SET HodometroInicial = -1 WHERE Id = @id",
        })
        {
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() =>
                banco.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@id", aluguel.Id)));
            Assert.AreEqual(547, erro.Number);
        }
        var erroCaucao = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            banco.Database.ExecuteSqlInterpolatedAsync($"UPDATE RevisoesContrato SET Caucao = 0 WHERE AluguelId = {aluguel.Id}"));
        Assert.AreEqual(547, erroCaucao.Number);
        var erroPreco = await Assert.ThrowsExactlyAsync<SqlException>(() =>
            banco.Database.ExecuteSqlInterpolatedAsync($"UPDATE RevisoesContrato SET Plano_PrecoKm = 1 WHERE AluguelId = {aluguel.Id}"));
        Assert.AreEqual(547, erroPreco.Number);
    }

    [TestMethod]
    public async Task DownSomenteEmCatalogoSemContratoPreservaCadastros()
    {
        var conexao = await AmbienteIntegracao.CriarCatalogoVazioAsync();
        try
        {
            await using (var banco = AmbienteIntegracao.CriarContexto(conexao))
                await banco.Database.MigrateAsync();
            await using var cenario = await CenarioLocacoesSql.CriarAsync(conexao);
            var migrations = cenario.Banco.Database.GetMigrations().ToArray();
            var f2 = migrations[Array.FindIndex(migrations, m => m.EndsWith("_ContratacaoFase3", StringComparison.Ordinal)) - 1];
            await cenario.Banco.GetService<IMigrator>().MigrateAsync(f2);
            Assert.AreEqual(1, await cenario.Banco.Clientes.CountAsync());
            await cenario.Banco.Database.MigrateAsync();
            Assert.AreEqual(0, await cenario.Banco.Alugueis.CountAsync());
        }
        finally
        {
            await AmbienteIntegracao.RemoverCatalogoAsync(conexao);
        }
    }
}
