using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class CadastrosFase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "OrdemVigenciaCombustivel");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AspNetUsers_EmpresaId_Id",
                table: "AspNetUsers",
                columns: new[] { "EmpresaId", "Id" });

            migrationBuilder.CreateTable(
                name: "AuditoriasCadastro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MomentoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Acao = table.Column<int>(type: "int", nullable: false),
                    Entidade = table.Column<int>(type: "int", nullable: false),
                    EntidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CamposAlterados = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriasCadastro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriasCadastro_AspNetUsers_EmpresaId_ContaId",
                        columns: x => new { x.EmpresaId, x.ContaId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_AuditoriasCadastro_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cpf = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CpfNormalizado = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    Cnpj = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CnpjNormalizado = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    Rg = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Cnh = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CnhNormalizada = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Endereco_Bairro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Endereco_Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Endereco_Estado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Endereco_Numero = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Endereco_Rua = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.UniqueConstraint("AK_Clientes_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_Clientes_Exclusao", "[ExcluidoEmUtc] IS NULL OR [ExcluidoEmUtc] = [AlteradoEmUtc]");
                    table.CheckConstraint("CK_Clientes_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.CheckConstraint("CK_Clientes_Tipo", "([Tipo] = 0 AND [Cpf] IS NOT NULL AND [CpfNormalizado] IS NOT NULL AND [Rg] IS NOT NULL AND [Cnh] IS NOT NULL AND [CnhNormalizada] IS NOT NULL AND [Cnpj] IS NULL AND [CnpjNormalizado] IS NULL) OR ([Tipo] = 1 AND [Cnpj] IS NOT NULL AND [CnpjNormalizado] IS NOT NULL AND [Cpf] IS NULL AND [CpfNormalizado] IS NULL AND [Rg] IS NULL AND [Cnh] IS NULL AND [CnhNormalizada] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Clientes_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Grupos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grupos", x => x.Id);
                    table.UniqueConstraint("AK_Grupos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_Grupos_Exclusao", "[ExcluidoEmUtc] IS NULL OR [ExcluidoEmUtc] = [AlteradoEmUtc]");
                    table.CheckConstraint("CK_Grupos_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.ForeignKey(
                        name: "FK_Grupos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrecosCombustiveis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    InicioVigenciaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Ordem = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [OrdemVigenciaCombustivel]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecosCombustiveis", x => x.Id);
                    table.UniqueConstraint("AK_PrecosCombustiveis_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_PrecosCombustiveis_PrecoTipo", "[PrecoUnitario] >= 0 AND [Tipo] IN (0,1,2,3)");
                    table.ForeignKey(
                        name: "FK_PrecosCombustiveis_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrecosSeguros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PrecoDiario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecosSeguros", x => x.Id);
                    table.UniqueConstraint("AK_PrecosSeguros_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_PrecosSeguros_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.CheckConstraint("CK_PrecosSeguros_PrecoTipo", "[PrecoDiario] >= 0 AND [Tipo] IN (0,1)");
                    table.ForeignKey(
                        name: "FK_PrecosSeguros_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaxasServicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    Modalidade = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxasServicos", x => x.Id);
                    table.UniqueConstraint("AK_TaxasServicos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_TaxasServicos_Exclusao", "[ExcluidoEmUtc] IS NULL OR [ExcluidoEmUtc] = [AlteradoEmUtc]");
                    table.CheckConstraint("CK_TaxasServicos_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.CheckConstraint("CK_TaxasServicos_PrecoModalidade", "[Preco] >= 0 AND [Modalidade] IN (0,1)");
                    table.ForeignKey(
                        name: "FK_TaxasServicos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Condutores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Cpf = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CpfNormalizado = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Cnh = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CnhNormalizada = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ValidadeCnh = table.Column<DateOnly>(type: "date", nullable: false),
                    Telefone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Condutores", x => x.Id);
                    table.UniqueConstraint("AK_Condutores_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_Condutores_Exclusao", "[ExcluidoEmUtc] IS NULL OR [ExcluidoEmUtc] = [AlteradoEmUtc]");
                    table.CheckConstraint("CK_Condutores_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.ForeignKey(
                        name: "FK_Condutores_Clientes_EmpresaId_ClienteId",
                        columns: x => new { x.EmpresaId, x.ClienteId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_Condutores_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VinculosClientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientePfId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientePjId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculosClientes", x => x.Id);
                    table.UniqueConstraint("AK_VinculosClientes_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_VinculosClientes_Distintos", "[ClientePfId] <> [ClientePjId]");
                    table.CheckConstraint("CK_VinculosClientes_Exclusao", "[ExcluidoEmUtc] IS NULL OR [ExcluidoEmUtc] = [AlteradoEmUtc]");
                    table.CheckConstraint("CK_VinculosClientes_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.ForeignKey(
                        name: "FK_VinculosClientes_Clientes_EmpresaId_ClientePfId",
                        columns: x => new { x.EmpresaId, x.ClientePfId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_VinculosClientes_Clientes_EmpresaId_ClientePjId",
                        columns: x => new { x.EmpresaId, x.ClientePjId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_VinculosClientes_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Planos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Modalidade = table.Column<int>(type: "int", nullable: false),
                    PrecoDiario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    PrecoKm = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    FranquiaDiariaKm = table.Column<int>(type: "int", nullable: true),
                    PrecoKmExcedente = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planos", x => x.Id);
                    table.UniqueConstraint("AK_Planos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_Planos_Exclusao", "[ExcluidoEmUtc] IS NULL OR [ExcluidoEmUtc] = [AlteradoEmUtc]");
                    table.CheckConstraint("CK_Planos_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.CheckConstraint("CK_Planos_Parametros", "[PrecoDiario] >= 0 AND (([Modalidade] = 0 AND [PrecoKm] IS NOT NULL AND [PrecoKm] >= 0 AND [FranquiaDiariaKm] IS NULL AND [PrecoKmExcedente] IS NULL) OR ([Modalidade] = 1 AND [PrecoKm] IS NULL AND [FranquiaDiariaKm] IS NOT NULL AND [FranquiaDiariaKm] >= 0 AND [PrecoKmExcedente] IS NOT NULL AND [PrecoKmExcedente] >= 0) OR ([Modalidade] = 2 AND [PrecoKm] IS NULL AND [FranquiaDiariaKm] IS NULL AND [PrecoKmExcedente] IS NULL))");
                    table.ForeignKey(
                        name: "FK_Planos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Planos_Grupos_EmpresaId_GrupoId",
                        columns: x => new { x.EmpresaId, x.GrupoId },
                        principalTable: "Grupos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "Veiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Placa = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PlacaNormalizada = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Combustivel = table.Column<int>(type: "int", nullable: false),
                    CapacidadeLitros = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Veiculos", x => x.Id);
                    table.UniqueConstraint("AK_Veiculos_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_Veiculos_Dados", "[CapacidadeLitros] > 0 AND [Ano] > 0 AND [Combustivel] IN (0,1,2,3)");
                    table.CheckConstraint("CK_Veiculos_Exclusao", "[ExcluidoEmUtc] IS NULL OR [ExcluidoEmUtc] = [AlteradoEmUtc]");
                    table.CheckConstraint("CK_Veiculos_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]");
                    table.ForeignKey(
                        name: "FK_Veiculos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Veiculos_Grupos_EmpresaId_GrupoId",
                        columns: x => new { x.EmpresaId, x.GrupoId },
                        principalTable: "Grupos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasCadastro_EmpresaId_ContaId",
                table: "AuditoriasCadastro",
                columns: new[] { "EmpresaId", "ContaId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasCadastro_EmpresaId_Entidade_EntidadeId_MomentoUtc",
                table: "AuditoriasCadastro",
                columns: new[] { "EmpresaId", "Entidade", "EntidadeId", "MomentoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_CnpjNormalizado",
                table: "Clientes",
                columns: new[] { "EmpresaId", "CnpjNormalizado" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL AND [CnpjNormalizado] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_CpfNormalizado",
                table: "Clientes",
                columns: new[] { "EmpresaId", "CpfNormalizado" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL AND [CpfNormalizado] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Condutores_EmpresaId_ClienteId",
                table: "Condutores",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Condutores_EmpresaId_CpfNormalizado",
                table: "Condutores",
                columns: new[] { "EmpresaId", "CpfNormalizado" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Planos_EmpresaId_GrupoId_Modalidade",
                table: "Planos",
                columns: new[] { "EmpresaId", "GrupoId", "Modalidade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrecosCombustiveis_EmpresaId_Tipo_InicioVigenciaUtc_Ordem_Id",
                table: "PrecosCombustiveis",
                columns: new[] { "EmpresaId", "Tipo", "InicioVigenciaUtc", "Ordem", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PrecosSeguros_EmpresaId_Tipo",
                table: "PrecosSeguros",
                columns: new[] { "EmpresaId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_EmpresaId_GrupoId_Id",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "GrupoId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_EmpresaId_PlacaNormalizada",
                table: "Veiculos",
                columns: new[] { "EmpresaId", "PlacaNormalizada" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VinculosClientes_EmpresaId_ClientePfId_ClientePjId",
                table: "VinculosClientes",
                columns: new[] { "EmpresaId", "ClientePfId", "ClientePjId" },
                unique: true,
                filter: "[ExcluidoEmUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VinculosClientes_EmpresaId_ClientePjId",
                table: "VinculosClientes",
                columns: new[] { "EmpresaId", "ClientePjId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriasCadastro");

            migrationBuilder.DropTable(
                name: "Condutores");

            migrationBuilder.DropTable(
                name: "Planos");

            migrationBuilder.DropTable(
                name: "PrecosCombustiveis");

            migrationBuilder.DropTable(
                name: "PrecosSeguros");

            migrationBuilder.DropTable(
                name: "TaxasServicos");

            migrationBuilder.DropTable(
                name: "Veiculos");

            migrationBuilder.DropTable(
                name: "VinculosClientes");

            migrationBuilder.DropTable(
                name: "Grupos");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AspNetUsers_EmpresaId_Id",
                table: "AspNetUsers");

            migrationBuilder.DropSequence(
                name: "OrdemVigenciaCombustivel");
        }
    }
}
