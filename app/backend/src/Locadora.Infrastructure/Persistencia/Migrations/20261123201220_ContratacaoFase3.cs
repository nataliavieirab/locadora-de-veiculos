using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Locadora.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ContratacaoFase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alugueis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CondutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaidaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetornoPrevistoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HodometroInicial = table.Column<int>(type: "int", nullable: false),
                    NumeroRevisaoAtual = table.Column<int>(type: "int", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlteradoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alugueis", x => x.Id);
                    table.UniqueConstraint("AK_Alugueis_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_Alugueis_Dados", "[Estado] = 0 AND [RetornoPrevistoUtc] > [SaidaUtc] AND [HodometroInicial] >= 0 AND [NumeroRevisaoAtual] > 0 AND [AlteradoEmUtc] >= [CriadoEmUtc] AND [SaidaUtc] = [CriadoEmUtc]");
                    table.ForeignKey(
                        name: "FK_Alugueis_Clientes_EmpresaId_ClienteId",
                        columns: x => new { x.EmpresaId, x.ClienteId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_Alugueis_Condutores_EmpresaId_CondutorId",
                        columns: x => new { x.EmpresaId, x.CondutorId },
                        principalTable: "Condutores",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_Alugueis_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Alugueis_Planos_EmpresaId_PlanoId",
                        columns: x => new { x.EmpresaId, x.PlanoId },
                        principalTable: "Planos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_Alugueis_Veiculos_EmpresaId_VeiculoId",
                        columns: x => new { x.EmpresaId, x.VeiculoId },
                        principalTable: "Veiculos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "RevisoesContrato",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AluguelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CondutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadaEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SaidaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetornoPrevistoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HodometroInicial = table.Column<int>(type: "int", nullable: false),
                    Caucao = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Cliente_Cnh = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Cliente_Cnpj = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Cliente_Cpf = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Cliente_Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Cliente_Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cliente_Rg = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Cliente_Telefone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Cliente_Tipo = table.Column<int>(type: "int", nullable: false),
                    Cliente_Endereco_Bairro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cliente_Endereco_Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cliente_Endereco_Estado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cliente_Endereco_Numero = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cliente_Endereco_Rua = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Condutor_Cnh = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Condutor_Cpf = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Condutor_Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Condutor_Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Condutor_Telefone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Condutor_ValidadeCnh = table.Column<DateOnly>(type: "date", nullable: false),
                    Plano_FranquiaDiariaKm = table.Column<int>(type: "int", nullable: true),
                    Plano_Modalidade = table.Column<int>(type: "int", nullable: false),
                    Plano_PrecoDiario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    Plano_PrecoKm = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    Plano_PrecoKmExcedente = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true),
                    Veiculo_Ano = table.Column<int>(type: "int", nullable: false),
                    Veiculo_CapacidadeLitros = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    Veiculo_Combustivel = table.Column<int>(type: "int", nullable: false),
                    Veiculo_Cor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Veiculo_GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Veiculo_GrupoNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Veiculo_Marca = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Veiculo_Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Veiculo_Placa = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisoesContrato", x => x.Id);
                    table.UniqueConstraint("AK_RevisoesContrato_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_RevisoesContrato_Cliente", "([Cliente_Tipo] = 0 AND [Cliente_Cpf] IS NOT NULL AND [Cliente_Rg] IS NOT NULL AND [Cliente_Cnh] IS NOT NULL AND [Cliente_Cnpj] IS NULL) OR ([Cliente_Tipo] = 1 AND [Cliente_Cnpj] IS NOT NULL AND [Cliente_Cpf] IS NULL AND [Cliente_Rg] IS NULL AND [Cliente_Cnh] IS NULL)");
                    table.CheckConstraint("CK_RevisoesContrato_Dados", "[Numero] > 0 AND [HodometroInicial] >= 0 AND [Caucao] = 1000.00 AND [RetornoPrevistoUtc] > [SaidaUtc] AND [CriadaEmUtc] >= [SaidaUtc] AND [GrupoId] = [Veiculo_GrupoId] AND [Veiculo_CapacidadeLitros] > 0 AND [Veiculo_Ano] > 0 AND [Veiculo_Combustivel] IN (0,1,2,3)");
                    table.CheckConstraint("CK_RevisoesContrato_Plano", "[Plano_PrecoDiario] >= 0 AND (([Plano_Modalidade] = 0 AND [Plano_PrecoKm] IS NOT NULL AND [Plano_PrecoKm] >= 0 AND [Plano_FranquiaDiariaKm] IS NULL AND [Plano_PrecoKmExcedente] IS NULL) OR ([Plano_Modalidade] = 1 AND [Plano_PrecoKm] IS NULL AND [Plano_FranquiaDiariaKm] IS NOT NULL AND [Plano_FranquiaDiariaKm] >= 0 AND [Plano_PrecoKmExcedente] IS NOT NULL AND [Plano_PrecoKmExcedente] >= 0) OR ([Plano_Modalidade] = 2 AND [Plano_PrecoKm] IS NULL AND [Plano_FranquiaDiariaKm] IS NULL AND [Plano_PrecoKmExcedente] IS NULL))");
                    table.ForeignKey(
                        name: "FK_RevisoesContrato_Alugueis_EmpresaId_AluguelId",
                        columns: x => new { x.EmpresaId, x.AluguelId },
                        principalTable: "Alugueis",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_RevisoesContrato_Clientes_EmpresaId_ClienteId",
                        columns: x => new { x.EmpresaId, x.ClienteId },
                        principalTable: "Clientes",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_RevisoesContrato_Condutores_EmpresaId_CondutorId",
                        columns: x => new { x.EmpresaId, x.CondutorId },
                        principalTable: "Condutores",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_RevisoesContrato_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RevisoesContrato_Grupos_EmpresaId_GrupoId",
                        columns: x => new { x.EmpresaId, x.GrupoId },
                        principalTable: "Grupos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_RevisoesContrato_Planos_EmpresaId_PlanoId",
                        columns: x => new { x.EmpresaId, x.PlanoId },
                        principalTable: "Planos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_RevisoesContrato_Veiculos_EmpresaId_VeiculoId",
                        columns: x => new { x.EmpresaId, x.VeiculoId },
                        principalTable: "Veiculos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "ComandosIdempotentes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operacao = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Chave = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HashComando = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    RevisaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoOriginal = table.Column<byte[]>(type: "binary(8)", fixedLength: true, maxLength: 8, nullable: false),
                    ConfirmadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComandosIdempotentes", x => x.Id);
                    table.UniqueConstraint("AK_ComandosIdempotentes_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_ComandosIdempotentes_Dados", "[Operacao] = N'criar-aluguel' AND LEN([HashComando]) = 64 AND [HashComando] NOT LIKE '%[^0-9A-F]%' COLLATE Latin1_General_100_BIN2 AND DATALENGTH([VersaoOriginal]) = 8");
                    table.ForeignKey(
                        name: "FK_ComandosIdempotentes_AspNetUsers_EmpresaId_ContaId",
                        columns: x => new { x.EmpresaId, x.ContaId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_ComandosIdempotentes_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComandosIdempotentes_RevisoesContrato_EmpresaId_RevisaoId",
                        columns: x => new { x.EmpresaId, x.RevisaoId },
                        principalTable: "RevisoesContrato",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "DemonstrativosEstimados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoRegra = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CalculadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SaidaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetornoPrevistoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Diarias = table.Column<int>(type: "int", nullable: false),
                    Quilometros = table.Column<int>(type: "int", nullable: true),
                    FranquiaTotalKm = table.Column<long>(type: "bigint", nullable: true),
                    QuilometragemPendente = table.Column<bool>(type: "bit", nullable: false),
                    CombustivelPendente = table.Column<bool>(type: "bit", nullable: false),
                    AtrasoPendente = table.Column<bool>(type: "bit", nullable: false),
                    TermoDiariasBruto = table.Column<decimal>(type: "decimal(28,9)", precision: 28, scale: 9, nullable: false),
                    TermoKmBruto = table.Column<decimal>(type: "decimal(28,9)", precision: 28, scale: 9, nullable: false),
                    TotalConhecido = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemonstrativosEstimados", x => x.Id);
                    table.UniqueConstraint("AK_DemonstrativosEstimados_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_DemonstrativosEstimados_Dados", "[Diarias] > 0 AND [Quilometros] IS NULL AND ([FranquiaTotalKm] IS NULL OR [FranquiaTotalKm] >= 0) AND [RetornoPrevistoUtc] > [SaidaUtc] AND [CalculadoEmUtc] >= [SaidaUtc] AND [TermoDiariasBruto] >= 0 AND [TermoKmBruto] = 0 AND [TotalConhecido] >= 0 AND [CombustivelPendente] = 1 AND [AtrasoPendente] = 1");
                    table.ForeignKey(
                        name: "FK_DemonstrativosEstimados_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DemonstrativosEstimados_RevisoesContrato_EmpresaId_RevisaoId",
                        columns: x => new { x.EmpresaId, x.RevisaoId },
                        principalTable: "RevisoesContrato",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "ItensContratados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    OrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxaServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrecoSeguroId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoSeguro = table.Column<int>(type: "int", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Modalidade = table.Column<int>(type: "int", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    OrigemInclusao = table.Column<int>(type: "int", nullable: false),
                    RevisaoPrimeiraInclusao = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensContratados", x => x.Id);
                    table.UniqueConstraint("AK_ItensContratados_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_ItensContratados_Dados", "[PrecoUnitario] >= 0 AND [Modalidade] IN (0,1) AND [OrigemInclusao] IN (0,1) AND [RevisaoPrimeiraInclusao] > 0 AND (([Categoria] = 0 AND [TaxaServicoId] IS NOT NULL AND [TaxaServicoId] = [OrigemId] AND [PrecoSeguroId] IS NULL AND [TipoSeguro] IS NULL) OR ([Categoria] = 1 AND [PrecoSeguroId] IS NOT NULL AND [PrecoSeguroId] = [OrigemId] AND [TaxaServicoId] IS NULL AND [TipoSeguro] IS NOT NULL AND [TipoSeguro] IN (0,1) AND [Modalidade] = 1))");
                    table.ForeignKey(
                        name: "FK_ItensContratados_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ItensContratados_PrecosSeguros_EmpresaId_PrecoSeguroId",
                        columns: x => new { x.EmpresaId, x.PrecoSeguroId },
                        principalTable: "PrecosSeguros",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_ItensContratados_RevisoesContrato_EmpresaId_RevisaoId",
                        columns: x => new { x.EmpresaId, x.RevisaoId },
                        principalTable: "RevisoesContrato",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_ItensContratados_TaxasServicos_EmpresaId_TaxaServicoId",
                        columns: x => new { x.EmpresaId, x.TaxaServicoId },
                        principalTable: "TaxasServicos",
                        principalColumns: new[] { "EmpresaId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "ComponentesEstimativa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DemonstrativoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: true),
                    OrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoSeguro = table.Column<int>(type: "int", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false),
                    Multiplicador = table.Column<int>(type: "int", nullable: false),
                    ValorBruto = table.Column<decimal>(type: "decimal(28,9)", precision: 28, scale: 9, nullable: false),
                    ValorArredondado = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentesEstimativa", x => x.Id);
                    table.UniqueConstraint("AK_ComponentesEstimativa_EmpresaId_Id", x => new { x.EmpresaId, x.Id });
                    table.CheckConstraint("CK_ComponentesEstimativa_Dados", "[Ordem] >= 0 AND [Multiplicador] > 0 AND [PrecoUnitario] >= 0 AND [ValorBruto] >= 0 AND [ValorArredondado] >= 0 AND (([Categoria] IS NULL AND [TipoSeguro] IS NULL AND [Ordem] = 0) OR ([Categoria] = 0 AND [TipoSeguro] IS NULL) OR ([Categoria] = 1 AND [TipoSeguro] IS NOT NULL AND [TipoSeguro] IN (0,1)))");
                    table.ForeignKey(
                        name: "FK_ComponentesEstimativa_DemonstrativosEstimados_EmpresaId_DemonstrativoId",
                        columns: x => new { x.EmpresaId, x.DemonstrativoId },
                        principalTable: "DemonstrativosEstimados",
                        principalColumns: new[] { "EmpresaId", "Id" });
                    table.ForeignKey(
                        name: "FK_ComponentesEstimativa_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_ClienteId",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_CondutorId",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "CondutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_Estado_SaidaUtc_Id",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "Estado", "SaidaUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_PlanoId",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "PlanoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_EmpresaId_VeiculoId",
                table: "Alugueis",
                columns: new[] { "EmpresaId", "VeiculoId" },
                unique: true,
                filter: "[Estado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComandosIdempotentes_EmpresaId_ContaId_Operacao_Chave",
                table: "ComandosIdempotentes",
                columns: new[] { "EmpresaId", "ContaId", "Operacao", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComandosIdempotentes_EmpresaId_RevisaoId",
                table: "ComandosIdempotentes",
                columns: new[] { "EmpresaId", "RevisaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesEstimativa_EmpresaId_DemonstrativoId_Ordem",
                table: "ComponentesEstimativa",
                columns: new[] { "EmpresaId", "DemonstrativoId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemonstrativosEstimados_EmpresaId_RevisaoId",
                table: "DemonstrativosEstimados",
                columns: new[] { "EmpresaId", "RevisaoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensContratados_EmpresaId_PrecoSeguroId",
                table: "ItensContratados",
                columns: new[] { "EmpresaId", "PrecoSeguroId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItensContratados_EmpresaId_RevisaoId_Categoria_OrigemId",
                table: "ItensContratados",
                columns: new[] { "EmpresaId", "RevisaoId", "Categoria", "OrigemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensContratados_EmpresaId_RevisaoId_TipoSeguro",
                table: "ItensContratados",
                columns: new[] { "EmpresaId", "RevisaoId", "TipoSeguro" },
                unique: true,
                filter: "[TipoSeguro] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItensContratados_EmpresaId_TaxaServicoId",
                table: "ItensContratados",
                columns: new[] { "EmpresaId", "TaxaServicoId" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisoesContrato_EmpresaId_AluguelId_Numero",
                table: "RevisoesContrato",
                columns: new[] { "EmpresaId", "AluguelId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisoesContrato_EmpresaId_ClienteId",
                table: "RevisoesContrato",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisoesContrato_EmpresaId_CondutorId",
                table: "RevisoesContrato",
                columns: new[] { "EmpresaId", "CondutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisoesContrato_EmpresaId_GrupoId",
                table: "RevisoesContrato",
                columns: new[] { "EmpresaId", "GrupoId" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisoesContrato_EmpresaId_PlanoId",
                table: "RevisoesContrato",
                columns: new[] { "EmpresaId", "PlanoId" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisoesContrato_EmpresaId_VeiculoId",
                table: "RevisoesContrato",
                columns: new[] { "EmpresaId", "VeiculoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComandosIdempotentes");

            migrationBuilder.DropTable(
                name: "ComponentesEstimativa");

            migrationBuilder.DropTable(
                name: "ItensContratados");

            migrationBuilder.DropTable(
                name: "DemonstrativosEstimados");

            migrationBuilder.DropTable(
                name: "RevisoesContrato");

            migrationBuilder.DropTable(
                name: "Alugueis");
        }
    }
}
