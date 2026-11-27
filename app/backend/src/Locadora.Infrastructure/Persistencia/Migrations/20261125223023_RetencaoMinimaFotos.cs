using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RetencaoMinimaFotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TrabalhosObjetos_Estado",
                table: "TrabalhosObjetos");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EnvioConfirmadoEmUtc",
                table: "TrabalhosObjetos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UltimaFalhaEmUtc",
                table: "TrabalhosObjetos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExcluirAposUtc",
                table: "FotosObjetos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovidaEmUtc",
                table: "FotosObjetos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetencaoLegada",
                table: "FotosObjetos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Um único piso para todo o legado; AlteradoEmUtc não comprova remoção.
            migrationBuilder.Sql("""
                DECLARE @piso datetimeoffset = DATEADD(day, 30, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'));
                UPDATE FotosObjetos SET RetencaoLegada=1, ExcluirAposUtc=@piso WHERE Estado=2;
                UPDATE TrabalhosObjetos SET
                    EnvioConfirmadoEmUtc=CASE WHEN CodigoFalha IN ('envio_confirmado','remocao_indisponivel')
                        OR Tipo=1 OR Estado=2 THEN AlteradoEmUtc ELSE NULL END,
                    UltimaFalhaEmUtc=CASE WHEN CodigoFalha IS NOT NULL AND CodigoFalha<>'envio_confirmado'
                        THEN AlteradoEmUtc ELSE NULL END,
                    Estado=CASE WHEN Estado=3 THEN 0 ELSE Estado END;
                UPDATE t SET Estado=0, ProximoProcessamentoUtc=f.ExcluirAposUtc
                    FROM TrabalhosObjetos t JOIN FotosObjetos f ON f.EmpresaId=t.EmpresaId AND f.Id=t.FotoObjetoId
                    WHERE f.Estado=2 AND t.Tipo=1 AND t.Estado=4;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TrabalhosObjetos_Estado",
                table: "TrabalhosObjetos",
                sql: "[Tipo] IN (0,1) AND [Estado] IN (0,1,2,4) AND [Tentativas] >= 0 AND ([Tipo] = 1 OR [Estado] <> 4)");

            migrationBuilder.CreateIndex(
                name: "IX_FotosObjetos_Estado_ExcluirAposUtc",
                table: "FotosObjetos",
                columns: new[] { "Estado", "ExcluirAposUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FotosObjetos_Retencao",
                table: "FotosObjetos",
                sql: "([Estado] <> 2 AND [RemovidaEmUtc] IS NULL AND [ExcluirAposUtc] IS NULL AND [RetencaoLegada]=0) OR ([Estado]=2 AND [ExcluirAposUtc] IS NOT NULL AND (([RetencaoLegada]=1 AND [RemovidaEmUtc] IS NULL) OR ([RetencaoLegada]=0 AND [RemovidaEmUtc] IS NOT NULL AND [ExcluirAposUtc]=DATEADD(day,30,[RemovidaEmUtc]))))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM FotosObjetos) OR EXISTS (SELECT 1 FROM TrabalhosObjetos)
                    THROW 51005, 'Rollback de fotos com evidencias bloqueado; corrigir adiante.', 1;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_TrabalhosObjetos_Estado",
                table: "TrabalhosObjetos");

            migrationBuilder.DropIndex(
                name: "IX_FotosObjetos_Estado_ExcluirAposUtc",
                table: "FotosObjetos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FotosObjetos_Retencao",
                table: "FotosObjetos");

            migrationBuilder.DropColumn(
                name: "EnvioConfirmadoEmUtc",
                table: "TrabalhosObjetos");

            migrationBuilder.DropColumn(
                name: "UltimaFalhaEmUtc",
                table: "TrabalhosObjetos");

            migrationBuilder.DropColumn(
                name: "ExcluirAposUtc",
                table: "FotosObjetos");

            migrationBuilder.DropColumn(
                name: "RemovidaEmUtc",
                table: "FotosObjetos");

            migrationBuilder.DropColumn(
                name: "RetencaoLegada",
                table: "FotosObjetos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TrabalhosObjetos_Estado",
                table: "TrabalhosObjetos",
                sql: "[Tipo] IN (0,1) AND [Estado] IN (0,1,2,3,4) AND [Tentativas] BETWEEN 0 AND 5 AND (([Tipo] = 1 AND [Estado] = 4) OR ([Tipo] = 0 AND [Estado] <> 4))");
        }
    }
}
