using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class FotosPrivadasFase5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FotoAtivaId",
                table: "Veiculos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FotosObjetos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Mime = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TamanhoBytes = table.Column<int>(type: "int", nullable: false),
                    Largura = table.Column<int>(type: "int", nullable: false),
                    Altura = table.Column<int>(type: "int", nullable: false),
                    Sha256 = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    Provedor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Bucket = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Caminho = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VersaoBase = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: false),
                    AtorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Correlacao = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosObjetos", x => x.Id);
                    table.UniqueConstraint("AK_FotosObjetos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.UniqueConstraint("AK_FotosObjetos_EmpresaId_VeiculoId_Id", x => new { x.EmpresaId, x.VeiculoId, x.Id });
                    table.CheckConstraint("CK_FotosObjetos_Estado", "[Estado] IN (0,1,2,3)");
                    table.CheckConstraint("CK_FotosObjetos_Imagem", "[Mime] IN ('image/jpeg','image/png') AND [TamanhoBytes] BETWEEN 1 AND 5242880 AND [Largura] > 0 AND [Altura] > 0 AND CAST([Largura] AS bigint)*[Altura] <= 16000000 AND DATALENGTH([Sha256]) = 32 AND DATALENGTH([VersaoBase]) = 8 AND [Provedor] IN ('Local','R2')");
                    table.CheckConstraint("CK_FotosObjetos_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.ForeignKey(
                        name: "FK_FotosObjetos_AspNetUsers_EmpresaId_AtorId",
                        columns: x => new { x.EmpresaId, x.AtorId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_FotosObjetos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FotosObjetos_Veiculos_EmpresaId_VeiculoId",
                        columns: x => new { x.EmpresaId, x.VeiculoId },
                        principalTable: "Veiculos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "TrabalhosObjetos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FotoObjetoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    TokenPosse = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseAteUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    ProximoProcessamentoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CodigoFalha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrabalhosObjetos", x => x.Id);
                    table.UniqueConstraint("AK_TrabalhosObjetos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_TrabalhosObjetos_Estado", "[Tipo] IN (0,1) AND [Estado] IN (0,1,2,3,4) AND [Tentativas] BETWEEN 0 AND 5 AND (([Tipo] = 1 AND [Estado] = 4) OR ([Tipo] = 0 AND [Estado] <> 4))");
                    table.CheckConstraint("CK_TrabalhosObjetos_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.CheckConstraint("CK_TrabalhosObjetos_Posse", "([Estado] = 1 AND [TokenPosse] IS NOT NULL AND [LeaseAteUtc] IS NOT NULL) OR ([Estado] <> 1 AND [TokenPosse] IS NULL AND [LeaseAteUtc] IS NULL)");
                    table.ForeignKey(
                        name: "FK_TrabalhosObjetos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TrabalhosObjetos_FotosObjetos_EmpresaId_FotoObjetoId",
                        columns: x => new { x.EmpresaId, x.FotoObjetoId },
                        principalTable: "FotosObjetos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_EmpresaId_Id_FotoAtivaId",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "Id", "FotoAtivaId" });

            migrationBuilder.CreateIndex(
                name: "IX_FotosObjetos_EmpresaId_AtorId",
                table: "FotosObjetos",
                columns: new[] { "EmpresaId", "AtorId" });

            migrationBuilder.CreateIndex(
                name: "IX_FotosObjetos_EmpresaId_VeiculoId",
                table: "FotosObjetos",
                columns: new[] { "EmpresaId", "VeiculoId" },
                unique: true,
                filter: "[Estado] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FotosObjetos_Provedor_Bucket_Chave",
                table: "FotosObjetos",
                columns: new[] { "Provedor", "Bucket", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrabalhosObjetos_EmpresaId_FotoObjetoId",
                table: "TrabalhosObjetos",
                columns: new[] { "EmpresaId", "FotoObjetoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrabalhosObjetos_Estado_ProximoProcessamentoUtc",
                table: "TrabalhosObjetos",
                columns: new[] { "Estado", "ProximoProcessamentoUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Veiculos_FotosObjetos_EmpresaId_Id_FotoAtivaId",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "Id", "FotoAtivaId" },
                principalTable: "FotosObjetos",
                principalColumns: new[] { "EmpresaId", "VeiculoId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não perder rastreabilidade de objetos/compensações em rollback de binário.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [FotosObjetos]) OR EXISTS (SELECT 1 FROM [TrabalhosObjetos])
                    THROW 51005, 'Rollback F5 bloqueado: preserve fotos e trabalhos; corrija adiante.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Veiculos_FotosObjetos_EmpresaId_Id_FotoAtivaId",
                table: "Veiculos");

            migrationBuilder.DropTable(
                name: "TrabalhosObjetos");

            migrationBuilder.DropTable(
                name: "FotosObjetos");

            migrationBuilder.DropIndex(
                name: "IX_Veiculos_EmpresaId_Id_FotoAtivaId",
                table: "Veiculos");

            migrationBuilder.DropColumn(
                name: "FotoAtivaId",
                table: "Veiculos");
        }
    }
}
