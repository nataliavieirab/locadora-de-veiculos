using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class DevolucaoFase4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ComandosIdempotentes_EmpresaId_RevisaoId",
                table: "ComandosIdempotentes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ComandosIdempotentes_Dados",
                table: "ComandosIdempotentes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Alugueis_Dados",
                table: "Alugueis");

            migrationBuilder.AddColumn<Guid>(
                name: "DemonstrativoFinalId",
                table: "ComandosIdempotentes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExcluidoEmUtc",
                table: "Alugueis",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_RevisoesContrato_EmpresaId_AluguelId_Id",
                table: "RevisoesContrato",
                columns: new[] { "EmpresaId", "AluguelId", "Id" });

            migrationBuilder.CreateTable(
                name: "DemonstrativosFinais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AluguelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaidaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetornoPrevistoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetornoRealUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HodometroInicial = table.Column<int>(type: "int", nullable: false),
                    HodometroFinal = table.Column<int>(type: "int", nullable: false),
                    LitrosRestantes = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    CapacidadeLitros = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    TipoCombustivel = table.Column<int>(type: "int", nullable: false),
                    PrecoCombustivelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioVigenciaCombustivelUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PrecoCombustivel = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    Diarias = table.Column<int>(type: "int", nullable: false),
                    Quilometros = table.Column<int>(type: "int", nullable: false),
                    FranquiaTotalKm = table.Column<long>(type: "bigint", nullable: true),
                    ExcedenteKm = table.Column<long>(type: "bigint", nullable: true),
                    TermoDiariasBruto = table.Column<decimal>(type: "decimal(28,9)", precision: 28, scale: 9, nullable: false),
                    TermoKmBruto = table.Column<decimal>(type: "decimal(28,9)", precision: 28, scale: 9, nullable: false),
                    TotalFinal = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Caucao = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    VersaoRegra = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CalculadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RegistradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Plano_FranquiaDiariaKm = table.Column<int>(type: "int", nullable: true),
                    Plano_Modalidade = table.Column<int>(type: "int", nullable: false),
                    Plano_PrecoDiario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    Plano_PrecoKm = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    Plano_PrecoKmExcedente = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemonstrativosFinais", x => x.Id);
                    table.UniqueConstraint("AK_DemonstrativosFinais_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.UniqueConstraint("AK_DemonstrativosFinais_EmpresaId_RevisaoId_Id", x => new { x.EmpresaId, x.RevisaoId, x.Id });
                    table.CheckConstraint("CK_DemonstrativosFinais_Dados", "[RetornoPrevistoUtc] > [SaidaUtc] AND [RetornoRealUtc] >= [SaidaUtc] AND [HodometroInicial] >= 0 AND [HodometroFinal] >= [HodometroInicial] AND [Quilometros] = [HodometroFinal] - [HodometroInicial] AND [LitrosRestantes] >= 0 AND [CapacidadeLitros] > 0 AND [LitrosRestantes] <= [CapacidadeLitros] AND [TipoCombustivel] IN (0,1,2,3) AND [PrecoCombustivel] >= 0 AND [InicioVigenciaCombustivelUtc] <= [RetornoRealUtc] AND [Diarias] >= 1 AND ([FranquiaTotalKm] IS NULL OR [FranquiaTotalKm] >= 0) AND ([ExcedenteKm] IS NULL OR [ExcedenteKm] >= 0) AND [TermoDiariasBruto] >= 0 AND [TermoKmBruto] >= 0 AND [TotalFinal] >= 0 AND [Caucao] = 1000 AND [CalculadoEmUtc] >= [SaidaUtc] AND [RegistradoEmUtc] >= [CalculadoEmUtc]");
                    table.CheckConstraint("CK_DemonstrativosFinais_Plano", "[Plano_PrecoDiario] >= 0 AND (([Plano_Modalidade] = 0 AND [Plano_PrecoKm] IS NOT NULL AND [Plano_PrecoKm] >= 0 AND [Plano_FranquiaDiariaKm] IS NULL AND [Plano_PrecoKmExcedente] IS NULL) OR ([Plano_Modalidade] = 1 AND [Plano_PrecoKm] IS NULL AND [Plano_FranquiaDiariaKm] IS NOT NULL AND [Plano_FranquiaDiariaKm] >= 0 AND [Plano_PrecoKmExcedente] IS NOT NULL AND [Plano_PrecoKmExcedente] >= 0) OR ([Plano_Modalidade] = 2 AND [Plano_PrecoKm] IS NULL AND [Plano_FranquiaDiariaKm] IS NULL AND [Plano_PrecoKmExcedente] IS NULL))");
                    table.ForeignKey(
                        name: "FK_DemonstrativosFinais_Alugueis_EmpresaId_AluguelId",
                        columns: x => new { x.EmpresaId, x.AluguelId },
                        principalTable: "Alugueis",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_DemonstrativosFinais_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DemonstrativosFinais_Planos_EmpresaId_PlanoId",
                        columns: x => new { x.EmpresaId, x.PlanoId },
                        principalTable: "Planos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_DemonstrativosFinais_PrecosCombustiveis_EmpresaId_PrecoCombustivelId",
                        columns: x => new { x.EmpresaId, x.PrecoCombustivelId },
                        principalTable: "PrecosCombustiveis",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_DemonstrativosFinais_RevisoesContrato_EmpresaId_AluguelId_RevisaoId",
                        columns: x => new { x.EmpresaId, x.AluguelId, x.RevisaoId },
                        principalTable: "RevisoesContrato",
                        principalColumns: new[] { "EmpresaId", "AluguelId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "ComponentesFinal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DemonstrativoFinalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    OrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoSeguro = table.Column<int>(type: "int", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Modalidade = table.Column<int>(type: "int", nullable: true),
                    OrigemInclusao = table.Column<int>(type: "int", nullable: true),
                    Quantidade = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    ValorBruto = table.Column<decimal>(type: "decimal(28,9)", precision: 28, scale: 9, nullable: false),
                    ValorArredondado = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentesFinal", x => x.Id);
                    table.UniqueConstraint("AK_ComponentesFinal_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_ComponentesFinal_Dados", "[Ordem] >= 0 AND [Quantidade] >= 0 AND [PrecoUnitario] >= 0 AND [ValorBruto] >= 0 AND [ValorArredondado] >= 0 AND (([Categoria] = 0 AND [OrigemId] IS NOT NULL AND [TipoSeguro] IS NULL AND [Modalidade] IS NULL AND [OrigemInclusao] IS NULL AND [Ordem] = 0) OR ([Categoria] = 1 AND [OrigemId] IS NOT NULL AND [TipoSeguro] IS NULL AND [Modalidade] IS NOT NULL AND [Modalidade] IN (0,1) AND [OrigemInclusao] IS NOT NULL AND [OrigemInclusao] IN (0,1,2)) OR ([Categoria] = 2 AND [OrigemId] IS NOT NULL AND [TipoSeguro] IS NOT NULL AND [TipoSeguro] IN (0,1) AND [Modalidade] = 1 AND [OrigemInclusao] IS NOT NULL AND [OrigemInclusao] IN (0,1)) OR ([Categoria] = 3 AND [OrigemId] IS NOT NULL AND [TipoSeguro] IS NULL AND [Modalidade] IS NULL AND [OrigemInclusao] IS NULL) OR ([Categoria] = 4 AND [OrigemId] IS NULL AND [TipoSeguro] IS NULL AND [Modalidade] IS NULL AND [OrigemInclusao] IS NULL))");
                    table.ForeignKey(
                        name: "FK_ComponentesFinal_DemonstrativosFinais_EmpresaId_DemonstrativoFinalId",
                        columns: x => new { x.EmpresaId, x.DemonstrativoFinalId },
                        principalTable: "DemonstrativosFinais",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_ComponentesFinal_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItensDevolucao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DemonstrativoFinalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxaServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Modalidade = table.Column<int>(type: "int", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    OrigemInclusao = table.Column<int>(type: "int", nullable: false),
                    VersaoTaxa = table.Column<byte[]>(type: "binary(8)", fixedLength: true, maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensDevolucao", x => x.Id);
                    table.UniqueConstraint("AK_ItensDevolucao_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_ItensDevolucao_Dados", "[PrecoUnitario] >= 0 AND [Modalidade] IN (0,1) AND [OrigemInclusao] = 2 AND DATALENGTH([VersaoTaxa]) = 8");
                    table.ForeignKey(
                        name: "FK_ItensDevolucao_DemonstrativosFinais_EmpresaId_DemonstrativoFinalId",
                        columns: x => new { x.EmpresaId, x.DemonstrativoFinalId },
                        principalTable: "DemonstrativosFinais",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_ItensDevolucao_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ItensDevolucao_TaxasServicos_EmpresaId_TaxaServicoId",
                        columns: x => new { x.EmpresaId, x.TaxaServicoId },
                        principalTable: "TaxasServicos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComandosIdempotentes_EmpresaId_RevisaoId_DemonstrativoFinalId",
                table: "ComandosIdempotentes",
                columns: new[] { "EmpresaId", "RevisaoId", "DemonstrativoFinalId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ComandosIdempotentes_Dados",
                table: "ComandosIdempotentes",
                sql: "(([Operacao] = N'criar-aluguel' AND [DemonstrativoFinalId] IS NULL) OR ([Operacao] = N'devolver-aluguel' AND [DemonstrativoFinalId] IS NOT NULL)) AND LEN([HashComando]) = 64 AND [HashComando] NOT LIKE '%[^0-9A-F]%' COLLATE Latin1_General_100_BIN2 AND DATALENGTH([VersaoOriginal]) = 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Alugueis_Dados",
                table: "Alugueis",
                sql: "[Estado] IN (0,1) AND ([ExcluidoEmUtc] IS NULL OR [Estado] = 1) AND [RetornoPrevistoUtc] > [SaidaUtc] AND [HodometroInicial] >= 0 AND [NumeroRevisaoAtual] > 0 AND [AlteradoEmUtc] >= [CriadoEmUtc] AND [SaidaUtc] = [CriadoEmUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesFinal_EmpresaId_DemonstrativoFinalId_Ordem",
                table: "ComponentesFinal",
                columns: new[] { "EmpresaId", "DemonstrativoFinalId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemonstrativosFinais_EmpresaId_AluguelId",
                table: "DemonstrativosFinais",
                columns: new[] { "EmpresaId", "AluguelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemonstrativosFinais_EmpresaId_AluguelId_RevisaoId",
                table: "DemonstrativosFinais",
                columns: new[] { "EmpresaId", "AluguelId", "RevisaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemonstrativosFinais_EmpresaId_PlanoId",
                table: "DemonstrativosFinais",
                columns: new[] { "EmpresaId", "PlanoId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemonstrativosFinais_EmpresaId_PrecoCombustivelId",
                table: "DemonstrativosFinais",
                columns: new[] { "EmpresaId", "PrecoCombustivelId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItensDevolucao_EmpresaId_DemonstrativoFinalId_TaxaServicoId",
                table: "ItensDevolucao",
                columns: new[] { "EmpresaId", "DemonstrativoFinalId", "TaxaServicoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensDevolucao_EmpresaId_TaxaServicoId",
                table: "ItensDevolucao",
                columns: new[] { "EmpresaId", "TaxaServicoId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ComandosIdempotentes_DemonstrativosFinais_EmpresaId_RevisaoId_DemonstrativoFinalId",
                table: "ComandosIdempotentes",
                columns: new[] { "EmpresaId", "RevisaoId", "DemonstrativoFinalId" },
                principalTable: "DemonstrativosFinais",
                principalColumns: new[] { "EmpresaId", "RevisaoId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [DemonstrativosFinais])
                    OR EXISTS (SELECT 1 FROM [Alugueis] WHERE [Estado] = 1 OR [ExcluidoEmUtc] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [ComandosIdempotentes] WHERE [Operacao] = N'devolver-aluguel')
                    THROW 51004, N'Rollback F4 bloqueado: preservar finais e corrigir adiante.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ComandosIdempotentes_DemonstrativosFinais_EmpresaId_RevisaoId_DemonstrativoFinalId",
                table: "ComandosIdempotentes");

            migrationBuilder.DropTable(
                name: "ComponentesFinal");

            migrationBuilder.DropTable(
                name: "ItensDevolucao");

            migrationBuilder.DropTable(
                name: "DemonstrativosFinais");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_RevisoesContrato_EmpresaId_AluguelId_Id",
                table: "RevisoesContrato");

            migrationBuilder.DropIndex(
                name: "IX_ComandosIdempotentes_EmpresaId_RevisaoId_DemonstrativoFinalId",
                table: "ComandosIdempotentes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ComandosIdempotentes_Dados",
                table: "ComandosIdempotentes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Alugueis_Dados",
                table: "Alugueis");

            migrationBuilder.DropColumn(
                name: "DemonstrativoFinalId",
                table: "ComandosIdempotentes");

            migrationBuilder.DropColumn(
                name: "ExcluidoEmUtc",
                table: "Alugueis");

            migrationBuilder.CreateIndex(
                name: "IX_ComandosIdempotentes_EmpresaId_RevisaoId",
                table: "ComandosIdempotentes",
                columns: new[] { "EmpresaId", "RevisaoId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ComandosIdempotentes_Dados",
                table: "ComandosIdempotentes",
                sql: "[Operacao] = N'criar-aluguel' AND LEN([HashComando]) = 64 AND [HashComando] NOT LIKE '%[^0-9A-F]%' COLLATE Latin1_General_100_BIN2 AND DATALENGTH([VersaoOriginal]) = 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Alugueis_Dados",
                table: "Alugueis",
                sql: "[Estado] = 0 AND [RetornoPrevistoUtc] > [SaidaUtc] AND [HodometroInicial] >= 0 AND [NumeroRevisaoAtual] > 0 AND [AlteradoEmUtc] >= [CriadoEmUtc] AND [SaidaUtc] = [CriadoEmUtc]");
        }
    }
}
