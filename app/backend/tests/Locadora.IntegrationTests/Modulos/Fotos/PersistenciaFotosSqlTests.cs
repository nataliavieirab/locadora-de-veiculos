using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.IntegrationTests.Modulos.Locacoes;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
public sealed class PersistenciaFotosSqlTests
{
    private static FotoObjeto Foto(CenarioLocacoesSql c, Veiculo? veiculo = null) =>
        FotoObjeto.Preparar(veiculo ?? c.Dados.Veiculo, "image/png", 100, 10, 10, new byte[32],
            (veiculo ?? c.Dados.Veiculo).RowVersion, ProvedorFoto.Local, "volume-local",
            c.Contexto.ContaId!.Value, "ensaio", CenarioLocacoesSql.Agora);

    private static async Task<FotoObjeto> Persistir(CenarioLocacoesSql c)
    {
        var foto = Foto(c);
        await using var tx = await c.Banco.Database.BeginTransactionAsync();
        c.Banco.Add(foto);
        c.Banco.Add(TrabalhoObjeto.Criar(foto, CenarioLocacoesSql.Agora));
        await c.Banco.SaveChangesAsync();
        await tx.CommitAsync();
        return foto;
    }

    [TestMethod]
    public async Task AtivacaoRetemMesmoTrabalhoSemDuplicarChaveOuPermitirExcluirAtivo()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var foto = await Persistir(c);
        var trabalho = await c.Banco.TrabalhosObjetos.SingleAsync();
        var versao = trabalho.RowVersion.ToArray();
        await using var tx = await c.Banco.Database.BeginTransactionAsync();
        var token = trabalho.Adquirir(CenarioLocacoesSql.Agora);
        c.Dados.Veiculo.DefinirFoto(foto, null, CenarioLocacoesSql.Agora);
        trabalho.Reter(token, foto, CenarioLocacoesSql.Agora);
        await c.Banco.SaveChangesAsync();
        await tx.CommitAsync();
        Assert.AreEqual(1, await c.Banco.TrabalhosObjetos.CountAsync());
        Assert.AreEqual(TipoTrabalhoObjeto.RemocaoRetida, trabalho.Tipo);
        Assert.IsFalse(versao.SequenceEqual(trabalho.RowVersion));
    }

    [TestMethod]
    public async Task PendenciaNaoAlteraRaizAtivacaoERemocaoGeramVersaoEHistoricoPermanece()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var veiculo = c.Dados.Veiculo;
        var versao = veiculo.RowVersion.ToArray();
        var foto = await Persistir(c);
        CollectionAssert.AreEqual(versao, veiculo.RowVersion);
        Assert.IsNull(veiculo.FotoAtivaId);
        Assert.AreEqual(8, foto.RowVersion.Length);
        await using (var tx = await c.Banco.Database.BeginTransactionAsync())
        {
            veiculo.DefinirFoto(foto, null, CenarioLocacoesSql.Agora.AddSeconds(1));
            await c.Banco.SaveChangesAsync();
            await tx.CommitAsync();
        }
        Assert.IsFalse(versao.SequenceEqual(veiculo.RowVersion));
        versao = veiculo.RowVersion.ToArray();
        await using (var tx = await c.Banco.Database.BeginTransactionAsync())
        {
            veiculo.RemoverFoto(foto, CenarioLocacoesSql.Agora.AddSeconds(2));
            await c.Banco.SaveChangesAsync();
            await tx.CommitAsync();
        }
        Assert.IsFalse(versao.SequenceEqual(veiculo.RowVersion));
        Assert.AreEqual(EstadoFoto.Inativa, (await c.Banco.FotosObjetos.SingleAsync()).Estado);
        Assert.IsNull(veiculo.FotoAtivaId);
    }

    [TestMethod]
    public async Task FiltrosFailClosedEGuardaNegamEmpresaExternaMetadadosEDestruicao()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var foto = await Persistir(a);
        await using var semEmpresa = AmbienteIntegracao.CriarContexto();
        Assert.AreEqual(0, await semEmpresa.FotosObjetos.CountAsync());
        Assert.AreEqual(0, await semEmpresa.TrabalhosObjetos.CountAsync());
        Assert.AreEqual(0, await b.Banco.FotosObjetos.CountAsync());
        await using (var tx = await b.Banco.Database.BeginTransactionAsync())
        {
            b.Banco.Add(Foto(a));
            await Assert.ThrowsExactlyAsync<EmpresaDivergenteException>(() => b.Banco.SaveChangesAsync());
        }
        await using (var tx = await a.Banco.Database.BeginTransactionAsync())
        {
            a.Banco.Entry(foto).Property(f => f.Chave).CurrentValue = "alterada";
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => a.Banco.SaveChangesAsync());
        }
        a.Banco.ChangeTracker.Clear();
        var lida = await a.Banco.FotosObjetos.SingleAsync();
        a.Banco.Remove(lida);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => a.Banco.SaveChangesAsync());
    }

    [TestMethod]
    public async Task FksSqlNegamReferenciaCruzadaEntreTenantsEEntreVeiculosDaMesmaEmpresa()
    {
        await using var a = await CenarioLocacoesSql.CriarAsync();
        await using var b = await CenarioLocacoesSql.CriarAsync();
        var fotoA = await Persistir(a);
        var fotoB = await Persistir(b);
        var segundo = Veiculo.Novo(a.Contexto.EmpresaId!.Value,
            new("FIC2B02", "Marca", "Azul", "Modelo", TipoCombustivel.Gasolina, 50, 2026, a.Dados.Grupo.Id),
            CenarioLocacoesSql.Agora);
        a.Banco.Add(segundo);
        await a.Banco.SaveChangesAsync();
        // SQL direto isola prova de FK da prova do interceptor/domínio.
        foreach (var (id, foto) in new[] { (a.Dados.Veiculo.Id, fotoB.Id), (segundo.Id, fotoA.Id) })
        {
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() => a.Banco.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Veiculos SET FotoAtivaId={foto} WHERE Id={id}"));
            Assert.AreEqual(547, erro.Number);
        }
        var erroFoto = await Assert.ThrowsExactlyAsync<SqlException>(() => a.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE FotosObjetos SET VeiculoId={b.Dados.Veiculo.Id} WHERE Id={fotoA.Id}"));
        Assert.AreEqual(547, erroFoto.Number);
        var erroTrabalho = await Assert.ThrowsExactlyAsync<SqlException>(() => a.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE TrabalhosObjetos SET FotoObjetoId={fotoB.Id} WHERE FotoObjetoId={fotoA.Id}"));
        Assert.AreEqual(547, erroTrabalho.Number);
    }

    [TestMethod]
    public async Task SqlGaranteUnicaAtivaChecksDeImagemELease()
    {
        await using var c = await CenarioLocacoesSql.CriarAsync();
        var foto = await Persistir(c);
        var segunda = Foto(c);
        await using (var tx = await c.Banco.Database.BeginTransactionAsync())
        {
            c.Banco.Add(segunda);
            await c.Banco.SaveChangesAsync();
            await tx.CommitAsync();
        }
        await c.Banco.Database.ExecuteSqlInterpolatedAsync($"UPDATE FotosObjetos SET Estado=1 WHERE Id={foto.Id}");
        var duplicada = await Assert.ThrowsExactlyAsync<SqlException>(() => c.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE FotosObjetos SET Estado=1 WHERE Id={segunda.Id}"));
        Assert.IsTrue(duplicada.Number is 2601 or 2627);
        foreach (var atribuicao in new[] { "TamanhoBytes=0", "TamanhoBytes=5242881", "Largura=16000001",
            "Altura=0", "Estado=99", "Sha256=0x01", "VersaoBase=0x01", "Mime='image/gif'", "Provedor='Externo'" })
        {
            var sql = $"UPDATE FotosObjetos SET {atribuicao} WHERE Id=@id"; // Somente literais da lista local.
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() => c.Banco.Database.ExecuteSqlRawAsync(
                sql, new SqlParameter("@id", foto.Id)));
            Assert.AreEqual(547, erro.Number);
        }
        var lease = await Assert.ThrowsExactlyAsync<SqlException>(() => c.Banco.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE TrabalhosObjetos SET Estado=1 WHERE FotoObjetoId={foto.Id}"));
        Assert.AreEqual(547, lease.Number);
    }

    [TestMethod]
    public async Task MigracaoF4PreservaContratoFinalEVersoesERollbackNaoApagaObjetos()
    {
        var conexao = await AmbienteIntegracao.CriarCatalogoVazioAsync();
        try
        {
            await using var destino = AmbienteIntegracao.CriarContexto(conexao);
            var idF4 = destino.GetService<IMigrationsAssembly>().Migrations.Keys
                .Last(m => string.CompareOrdinal(m, "20261125151512") < 0);
            await destino.GetService<IMigrator>().MigrateAsync(idF4);
            await using var origem = await CenarioLocacoesSql.CriarAsync();
            var aluguel = await PersistenciaDevolucaoSqlTests.CriarAbertoAsync(origem);
            await PersistenciaDevolucaoSqlTests.FecharAsync(origem, aluguel.Id,
                await PersistenciaDevolucaoSqlTests.PrepararDadosAsync(origem));
            var catalogo = new SqlConnectionStringBuilder(origem.Banco.Database.GetConnectionString()).InitialCatalog;
            Assert.IsTrue(catalogo.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'));
            foreach (var tabela in new[] { "Empresas", "AspNetUsers", "Grupos", "Planos", "Veiculos", "Clientes",
                "Condutores", "TaxasServicos", "PrecosSeguros", "PrecosCombustiveis", "Alugueis", "RevisoesContrato",
                "ItensContratados", "DemonstrativosEstimados", "ComponentesEstimativa",
                "DemonstrativosFinais", "ItensDevolucao", "ComponentesFinal", "ComandosIdempotentes" })
            {
                var colunas = await destino.Database.SqlQuery<string>(
                    $"SELECT QUOTENAME(name) AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID({tabela}) AND system_type_id<>189 ORDER BY column_id").ToListAsync();
                var lista = string.Join(",", colunas);
                var campo = tabela == "Empresas" ? "Id" : "EmpresaId";
                var sql = $"INSERT INTO [{tabela}] ({lista}) SELECT {lista} FROM [{catalogo}].dbo.[{tabela}] WHERE [{campo}]=@empresa";
                await destino.Database.ExecuteSqlRawAsync(
                    sql,
                    new SqlParameter("@empresa", origem.Contexto.EmpresaId!.Value));
            }
            var antes = await destino.Database.SqlQuery<string>(
                $"SELECT (SELECT * FROM Alugueis WHERE Id={aluguel.Id} FOR JSON PATH) AS [Value]").SingleAsync();
            var finalAntes = await destino.Database.SqlQuery<string>(
                $"SELECT (SELECT * FROM DemonstrativosFinais WHERE AluguelId={aluguel.Id} FOR JSON PATH) AS [Value]").SingleAsync();
            await destino.Database.MigrateAsync();
            Assert.AreEqual(antes, await destino.Database.SqlQuery<string>(
                $"SELECT (SELECT * FROM Alugueis WHERE Id={aluguel.Id} FOR JSON PATH) AS [Value]").SingleAsync());
            Assert.AreEqual(finalAntes, await destino.Database.SqlQuery<string>(
                $"SELECT (SELECT * FROM DemonstrativosFinais WHERE AluguelId={aluguel.Id} FOR JSON PATH) AS [Value]").SingleAsync());
            await using var atualizado = AmbienteIntegracao.CriarContextoComGuarda(origem.Contexto, conexao);
            var veiculo = await atualizado.Veiculos.SingleAsync();
            Assert.IsNull(veiculo.FotoAtivaId);
            var foto = FotoObjeto.Preparar(veiculo, "image/png", 1, 1, 1, new byte[32], veiculo.RowVersion,
                ProvedorFoto.Local, "local", origem.Contexto.ContaId!.Value, "teste", CenarioLocacoesSql.Agora);
            await using (var tx = await atualizado.Database.BeginTransactionAsync())
            {
                atualizado.Add(foto);
                await atualizado.SaveChangesAsync();
                await tx.CommitAsync();
            }
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() => destino.GetService<IMigrator>().MigrateAsync(idF4));
            Assert.AreEqual(51005, erro.Number);
            Assert.AreEqual(1, await atualizado.FotosObjetos.CountAsync());
        }
        finally { await AmbienteIntegracao.RemoverCatalogoAsync(conexao); }
    }
}
