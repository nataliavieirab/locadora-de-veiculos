using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Fotos;

internal static class GuardaReferenciasFotos
{
    // Whitelist do schema conhecido, não uma conclusão por ausência de referência corrente.
    public static async Task<bool> PermiteAsync(LocadoraDbContext banco, FotoObjeto foto, CancellationToken token)
    {
        // sys.* pode ocultar objetos sem permissão, em vez de lançar erro.
        // Metadados parcialmente visíveis não provam ausência de dependência.
        var visibilidade = await banco.Database.SqlQuery<int>($"""
            SELECT COALESCE(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','VIEW DEFINITION'),0) AS [Value]
            """).SingleAsync(token);
        if (visibilidade != 1) return false;
        var entradas = await banco.Database.SqlQuery<string>($"""
            SELECT CONCAT(OBJECT_SCHEMA_NAME(k.parent_object_id),'.',OBJECT_NAME(k.parent_object_id),'.',p.name,
                '>',r.name,':',k.is_disabled,':',k.is_not_trusted) AS [Value]
            FROM sys.foreign_keys k JOIN sys.foreign_key_columns c ON k.object_id=c.constraint_object_id
            JOIN sys.columns p ON p.object_id=c.parent_object_id AND p.column_id=c.parent_column_id
            JOIN sys.columns r ON r.object_id=c.referenced_object_id AND r.column_id=c.referenced_column_id
            WHERE k.referenced_object_id=OBJECT_ID('dbo.FotosObjetos')
            """).ToListAsync(token);
        var conhecidas = new[] {
            "dbo.Veiculos.EmpresaId>EmpresaId:0:0", "dbo.Veiculos.Id>VeiculoId:0:0", "dbo.Veiculos.FotoAtivaId>Id:0:0",
            "dbo.TrabalhosObjetos.EmpresaId>EmpresaId:0:0", "dbo.TrabalhosObjetos.FotoObjetoId>Id:0:0"
        };
        if (!entradas.Order().SequenceEqual(conhecidas.Order())) return false;
        var dependencias = await banco.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.sql_expression_dependencies
            WHERE referenced_id=OBJECT_ID('dbo.FotosObjetos')
                AND referencing_id NOT IN (OBJECT_ID('dbo.FotosObjetos'),OBJECT_ID('dbo.TrabalhosObjetos'),OBJECT_ID('dbo.Veiculos'))
                AND referencing_id NOT IN (SELECT object_id FROM sys.foreign_keys
                    WHERE referenced_object_id=OBJECT_ID('dbo.FotosObjetos'))
                AND referencing_id NOT IN (SELECT object_id FROM sys.check_constraints
                    WHERE parent_object_id=OBJECT_ID('dbo.FotosObjetos') AND is_disabled=0 AND is_not_trusted=0
                        AND name IN ('CK_FotosObjetos_Estado','CK_FotosObjetos_Retencao','CK_FotosObjetos_Imagem','CK_FotosObjetos_Instantes'))
            """).SingleAsync(token);
        if (dependencias != 0) return false;
        var desconhecidas = await banco.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
            WHERE c.name LIKE '%Foto%' AND NOT
                (SCHEMA_NAME(t.schema_id)='dbo' AND
                    ((t.name='Veiculos' AND c.name='FotoAtivaId') OR
                     (t.name='TrabalhosObjetos' AND c.name='FotoObjetoId')))
            """).SingleAsync(token);
        if (desconhecidas != 0) return false;
        foreach (var tipo in new[] { typeof(FotoObjeto), typeof(TrabalhoObjeto), typeof(Domain.Modulos.FrotaPrecos.Veiculo) })
        {
            var entidade = banco.Model.FindEntityType(tipo)!;
            var tabela = entidade.GetTableName()!;
            var colunas = await banco.Database.SqlQuery<string>(
                $"SELECT name AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID({tabela})").ToListAsync(token);
            var mapeadas = entidade.GetProperties().Select(p => p.GetColumnName()).Order();
            if (!colunas.Order().SequenceEqual(mapeadas)) return false;
        }
        // Inclui veículo excluído. Relação histórica nova nunca é ignorada.
        return !await banco.Veiculos.IgnoreQueryFilters().AnyAsync(
            v => v.EmpresaId == foto.EmpresaId && v.FotoAtivaId == foto.Id, token);
    }
}
