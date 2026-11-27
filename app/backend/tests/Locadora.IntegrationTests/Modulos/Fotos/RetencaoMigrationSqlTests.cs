using Locadora.Domain.Modulos.Fotos;
using Locadora.IntegrationTests.Modulos.Locacoes;
using Locadora.IntegrationTests.Suporte;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Locadora.IntegrationTests.Modulos.Fotos;

[TestClass]
public sealed class RetencaoMigrationSqlTests
{
    [TestMethod]
    public async Task UpgradeF5PreservaLegadoEReabreFalhasSemInventarRemocao()
    {
        var conexao = await AmbienteIntegracao.CriarCatalogoVazioAsync();
        try
        {
            await using var destino = AmbienteIntegracao.CriarContexto(conexao);
            await destino.GetService<IMigrator>().MigrateAsync("20261125151512_FotosPrivadasFase5");
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
                await destino.Database.ExecuteSqlRawAsync(sql,
                    new SqlParameter("@empresa", origem.Contexto.EmpresaId!.Value));
            }
            var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
            var empresa = origem.Contexto.EmpresaId!.Value;
            var agora = CenarioLocacoesSql.Agora;
            for (var estado = 0; estado < 4; estado++)
            {
                var id = ids[estado];
                var chave = $"{empresa:N}/{id:N}";
                var codigo = estado == 3 ? "remocao_indisponivel" : estado == 0 ? "armazenamento_indisponivel" : "envio_confirmado";
                var tipo = estado is 1 or 2 ? 1 : 0;
                var trabalhoEstado = tipo == 1 ? 4 : 3;
                await destino.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT FotosObjetos (Id,EmpresaId,VeiculoId,Estado,Mime,TamanhoBytes,Largura,Altura,Sha256,Provedor,Bucket,Chave,Caminho,VersaoBase,AtorId,Correlacao,CriadoEmUtc,AlteradoEmUtc)
                    VALUES ({id},{empresa},{origem.Dados.Veiculo.Id},{estado},'image/png',1,1,1,{new byte[32]},'Local','local',{chave},'/api/v1/foto',{new byte[8]},{origem.Contexto.ContaId!.Value},'legado',{agora},{agora});
                    INSERT TrabalhosObjetos (Id,EmpresaId,FotoObjetoId,Tipo,Estado,Tentativas,ProximoProcessamentoUtc,CodigoFalha,CriadoEmUtc,AlteradoEmUtc)
                    VALUES ({Guid.NewGuid()},{empresa},{id},{tipo},{trabalhoEstado},5,{agora},{codigo},{agora},{agora});
                    """);
            }
            var contratoAntes = await destino.Database.SqlQuery<string>(
                $"SELECT (SELECT * FROM Alugueis WHERE Id={aluguel.Id} FOR JSON PATH) AS [Value]").SingleAsync();
            var finalAntes = await destino.Database.SqlQuery<string>(
                $"SELECT (SELECT * FROM DemonstrativosFinais WHERE AluguelId={aluguel.Id} FOR JSON PATH) AS [Value]").SingleAsync();
            var antes = DateTimeOffset.UtcNow;
            await destino.Database.MigrateAsync();
            var depois = DateTimeOffset.UtcNow;
            Assert.AreEqual(contratoAntes, await destino.Database.SqlQuery<string>(
                $"SELECT (SELECT * FROM Alugueis WHERE Id={aluguel.Id} FOR JSON PATH) AS [Value]").SingleAsync());
            Assert.AreEqual(finalAntes, await destino.Database.SqlQuery<string>(
                $"SELECT (SELECT * FROM DemonstrativosFinais WHERE AluguelId={aluguel.Id} FOR JSON PATH) AS [Value]").SingleAsync());
            await using var banco = AmbienteIntegracao.CriarContextoComGuarda(origem.Contexto, conexao);
            var fotos = await banco.FotosObjetos.ToListAsync();
            foreach (var foto in fotos)
            {
                Assert.IsNull(foto.RemovidaEmUtc);
                Assert.AreEqual(8, foto.RowVersion.Length);
                if (foto.Estado == EstadoFoto.Inativa)
                {
                    Assert.IsTrue(foto.RetencaoLegada);
                    Assert.IsTrue(foto.ExcluirAposUtc >= antes.AddDays(30) && foto.ExcluirAposUtc <= depois.AddDays(30));
                    Assert.IsFalse(foto.ElegivelParaRemocao(antes));
                }
                else Assert.IsNull(foto.ExcluirAposUtc);
            }
            var trabalhos = await banco.TrabalhosObjetos.ToListAsync();
            Assert.IsTrue(trabalhos.All(t => t.Tentativas == 5 && t.CodigoFalha is not null));
            Assert.IsTrue(trabalhos.All(t => t.Estado != EstadoTrabalhoObjeto.FalhaFinal));
            Assert.IsFalse(trabalhos.Single(t => t.FotoObjetoId == ids[0]).EnvioConfirmado);
            Assert.IsTrue(trabalhos.Single(t => t.FotoObjetoId == ids[3]).EnvioConfirmado);
            Assert.AreEqual("remocao_indisponivel", trabalhos.Single(t => t.FotoObjetoId == ids[3]).CodigoFalha);
            Assert.AreEqual(fotos.Single(f => f.Id == ids[2]).ExcluirAposUtc,
                trabalhos.Single(t => t.FotoObjetoId == ids[2]).ProximoProcessamentoUtc);
            var inativa = fotos.Single(f => f.Id == ids[2]);
            await using (var tx = await banco.Database.BeginTransactionAsync())
            {
                banco.Entry(inativa).Property(f => f.ExcluirAposUtc).CurrentValue = depois.AddDays(60);
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => banco.SaveChangesAsync());
            }
            var erro = await Assert.ThrowsExactlyAsync<SqlException>(() => destino.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE FotosObjetos SET ExcluirAposUtc={depois} WHERE Id={ids[1]}"));
            Assert.AreEqual(547, erro.Number);
            await Assert.ThrowsExactlyAsync<SqlException>(() =>
                destino.GetService<IMigrator>().MigrateAsync("20261125151512_FotosPrivadasFase5"));
        }
        finally { await AmbienteIntegracao.RemoverCatalogoAsync(conexao); }
    }
}
