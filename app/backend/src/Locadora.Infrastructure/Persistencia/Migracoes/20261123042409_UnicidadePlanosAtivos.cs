using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class UnicidadePlanosAtivos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Planos_EmpresaId_GrupoId_Modalidade",
                table: "Planos");

            migrationBuilder.CreateIndex(
                name: "IX_Planos_EmpresaId_GrupoId_Modalidade",
                table: "Planos",
                columns: new[] { "EmpresaId", "GrupoId", "Modalidade" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Planos_EmpresaId_GrupoId_Modalidade",
                table: "Planos");

            migrationBuilder.CreateIndex(
                name: "IX_Planos_EmpresaId_GrupoId_Modalidade",
                table: "Planos",
                columns: new[] { "EmpresaId", "GrupoId", "Modalidade" },
                unique: true);
        }
    }
}
