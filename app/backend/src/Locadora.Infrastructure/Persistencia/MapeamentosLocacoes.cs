using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Locadora.Infrastructure.Persistencia;

internal static class MapeamentosLocacoes
{
    private const string ParametrosPlano =
        "[Plano_PrecoDiario] >= 0 AND (([Plano_Modalidade] = 0 AND [Plano_PrecoKm] IS NOT NULL AND [Plano_PrecoKm] >= 0 AND [Plano_FranquiaDiariaKm] IS NULL AND [Plano_PrecoKmExcedente] IS NULL) OR " +
        "([Plano_Modalidade] = 1 AND [Plano_PrecoKm] IS NULL AND [Plano_FranquiaDiariaKm] IS NOT NULL AND [Plano_FranquiaDiariaKm] >= 0 AND [Plano_PrecoKmExcedente] IS NOT NULL AND [Plano_PrecoKmExcedente] >= 0) OR " +
        "([Plano_Modalidade] = 2 AND [Plano_PrecoKm] IS NULL AND [Plano_FranquiaDiariaKm] IS NULL AND [Plano_PrecoKmExcedente] IS NULL))";

    public static void Aplicar(ModelBuilder builder)
    {
        var aluguel = builder.Entity<Aluguel>();
        aluguel.HasBaseType((Type?)null);
        aluguel.ToTable("Alugueis", t =>
        {
            t.HasCheckConstraint("CK_Alugueis_Dados", "[Estado] IN (0,1) AND ([ExcluidoEmUtc] IS NULL OR [Estado] = 1) AND [RetornoPrevistoUtc] > [SaidaUtc] AND [HodometroInicial] >= 0 AND [NumeroRevisaoAtual] > 0 AND [AlteradoEmUtc] >= [CriadoEmUtc] AND [SaidaUtc] = [CriadoEmUtc]");
        });
        aluguel.HasKey(a => a.Id);
        aluguel.Property(a => a.Id).ValueGeneratedNever();
        aluguel.HasAlternateKey(a => new { a.EmpresaId, a.Id });
        aluguel.Property(a => a.RowVersion).IsRowVersion();
        aluguel.Ignore(a => a.RevisaoAtual);
        aluguel.HasOne<Empresa>().WithMany().HasForeignKey(a => a.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        aluguel.HasOne<Cliente>().WithMany().HasForeignKey(a => new { a.EmpresaId, a.ClienteId }).HasPrincipalKey(c => new { c.EmpresaId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        aluguel.HasOne<Condutor>().WithMany().HasForeignKey(a => new { a.EmpresaId, a.CondutorId }).HasPrincipalKey(c => new { c.EmpresaId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        aluguel.HasOne<Veiculo>().WithMany().HasForeignKey(a => new { a.EmpresaId, a.VeiculoId }).HasPrincipalKey(c => new { c.EmpresaId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        aluguel.HasOne<Plano>().WithMany().HasForeignKey(a => new { a.EmpresaId, a.PlanoId }).HasPrincipalKey(c => new { c.EmpresaId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        aluguel.HasIndex(a => new { a.EmpresaId, a.VeiculoId }).IsUnique().HasFilter("[Estado] = 0");
        aluguel.HasIndex(a => new { a.EmpresaId, a.Estado, a.SaidaUtc, a.Id });

        var revisao = Imutavel<RevisaoContrato>(builder, "RevisoesContrato");
        revisao.HasAlternateKey(r => new { r.EmpresaId, r.AluguelId, r.Id });
        revisao.HasIndex(r => new { r.EmpresaId, r.AluguelId, r.Numero }).IsUnique();
        aluguel.HasMany(a => a.Revisoes).WithOne().HasForeignKey(r => new { r.EmpresaId, r.AluguelId })
            .HasPrincipalKey(a => new { a.EmpresaId, a.Id }).OnDelete(DeleteBehavior.NoAction);
        aluguel.Navigation(a => a.Revisoes).HasField("_revisoes").UsePropertyAccessMode(PropertyAccessMode.Field);
        revisao.HasOne<Cliente>().WithMany().HasForeignKey(r => new { r.EmpresaId, r.ClienteId }).HasPrincipalKey(c => new { c.EmpresaId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        revisao.HasOne<Condutor>().WithMany().HasForeignKey(r => new { r.EmpresaId, r.CondutorId }).HasPrincipalKey(c => new { c.EmpresaId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        revisao.HasOne<Veiculo>().WithMany().HasForeignKey(r => new { r.EmpresaId, r.VeiculoId }).HasPrincipalKey(c => new { c.EmpresaId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        revisao.HasOne<Plano>().WithMany().HasForeignKey(r => new { r.EmpresaId, r.PlanoId }).HasPrincipalKey(c => new { c.EmpresaId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        revisao.HasOne<Grupo>().WithMany().HasForeignKey(r => new { r.EmpresaId, r.GrupoId }).HasPrincipalKey(c => new { c.EmpresaId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        revisao.Property(r => r.Caucao).HasPrecision(19, 2);
        revisao.ToTable(t =>
        {
            t.HasCheckConstraint("CK_RevisoesContrato_Dados", "[Numero] > 0 AND [HodometroInicial] >= 0 AND [Caucao] = 1000.00 AND [RetornoPrevistoUtc] > [SaidaUtc] AND [CriadaEmUtc] >= [SaidaUtc] AND [GrupoId] = [Veiculo_GrupoId] AND [Veiculo_CapacidadeLitros] > 0 AND [Veiculo_Ano] > 0 AND [Veiculo_Combustivel] IN (0,1,2,3)");
            t.HasCheckConstraint("CK_RevisoesContrato_Plano", ParametrosPlano);
            t.HasCheckConstraint("CK_RevisoesContrato_Cliente", "([Cliente_Tipo] = 0 AND [Cliente_Cpf] IS NOT NULL AND [Cliente_Rg] IS NOT NULL AND [Cliente_Cnh] IS NOT NULL AND [Cliente_Cnpj] IS NULL) OR ([Cliente_Tipo] = 1 AND [Cliente_Cnpj] IS NOT NULL AND [Cliente_Cpf] IS NULL AND [Cliente_Rg] IS NULL AND [Cliente_Cnh] IS NULL)");
        });
        revisao.ComplexProperty(r => r.Cliente, c =>
        {
            c.IsRequired();
            c.Property(s => s.Nome).HasMaxLength(200).IsRequired();
            c.Property(s => s.Cpf).HasMaxLength(64);
            c.Property(s => s.Cnpj).HasMaxLength(64);
            c.Property(s => s.Rg).HasMaxLength(64);
            c.Property(s => s.Cnh).HasMaxLength(64);
            c.Property(s => s.Telefone).HasMaxLength(32).IsRequired();
            c.Property(s => s.Email).HasMaxLength(254);
            c.ComplexProperty(s => s.Endereco, e =>
            {
                e.IsRequired();
                e.Property(s => s.Estado).HasMaxLength(100).IsRequired();
                e.Property(s => s.Cidade).HasMaxLength(100).IsRequired();
                e.Property(s => s.Bairro).HasMaxLength(100).IsRequired();
                e.Property(s => s.Rua).HasMaxLength(200).IsRequired();
                e.Property(s => s.Numero).HasMaxLength(200).IsRequired();
            });
        });
        revisao.ComplexProperty(r => r.Condutor, c =>
        {
            c.IsRequired();
            c.Property(s => s.Nome).HasMaxLength(200).IsRequired();
            c.Property(s => s.Cpf).HasMaxLength(64).IsRequired();
            c.Property(s => s.Cnh).HasMaxLength(64).IsRequired();
            c.Property(s => s.ValidadeCnh).HasColumnType("date");
            c.Property(s => s.Email).HasMaxLength(254).IsRequired();
            c.Property(s => s.Telefone).HasMaxLength(32).IsRequired();
        });
        revisao.ComplexProperty(r => r.Veiculo, v =>
        {
            v.IsRequired();
            v.Property(s => s.Placa).HasMaxLength(16).IsRequired();
            v.Property(s => s.Marca).HasMaxLength(100).IsRequired();
            v.Property(s => s.Modelo).HasMaxLength(100).IsRequired();
            v.Property(s => s.Cor).HasMaxLength(100).IsRequired();
            v.Property(s => s.GrupoNome).HasMaxLength(200).IsRequired();
            v.Property(s => s.CapacidadeLitros).HasPrecision(12, 3);
        });
        revisao.ComplexProperty(r => r.Plano, p =>
        {
            p.IsRequired();
            p.Property(s => s.PrecoDiario).HasPrecision(19, 6);
            p.Property(s => s.PrecoKm).HasPrecision(19, 6);
            p.Property(s => s.PrecoKmExcedente).HasPrecision(19, 6);
        });

        var item = Imutavel<ItemContratado>(builder, "ItensContratados");
        revisao.HasMany(r => r.Itens).WithOne().HasForeignKey(i => new { i.EmpresaId, i.RevisaoId })
            .HasPrincipalKey(r => new { r.EmpresaId, r.Id }).OnDelete(DeleteBehavior.NoAction);
        revisao.Navigation(r => r.Itens).HasField("_itens").UsePropertyAccessMode(PropertyAccessMode.Field);
        item.HasOne<TaxaServico>().WithMany().HasForeignKey(i => new { i.EmpresaId, i.TaxaServicoId }).HasPrincipalKey(t => new { t.EmpresaId, t.Id }).OnDelete(DeleteBehavior.NoAction);
        item.HasOne<PrecoSeguro>().WithMany().HasForeignKey(i => new { i.EmpresaId, i.PrecoSeguroId }).HasPrincipalKey(s => new { s.EmpresaId, s.Id }).OnDelete(DeleteBehavior.NoAction);
        item.HasIndex(i => new { i.EmpresaId, i.RevisaoId, i.Categoria, i.OrigemId }).IsUnique();
        item.HasIndex(i => new { i.EmpresaId, i.RevisaoId, i.TipoSeguro }).IsUnique().HasFilter("[TipoSeguro] IS NOT NULL");
        item.Property(i => i.Nome).HasMaxLength(200).IsRequired();
        item.Property(i => i.PrecoUnitario).HasPrecision(19, 6);
        item.ToTable(t => t.HasCheckConstraint("CK_ItensContratados_Dados",
            "[PrecoUnitario] >= 0 AND [Modalidade] IN (0,1) AND [OrigemInclusao] IN (0,1) AND [RevisaoPrimeiraInclusao] > 0 AND " +
            "(([Categoria] = 0 AND [TaxaServicoId] IS NOT NULL AND [TaxaServicoId] = [OrigemId] AND [PrecoSeguroId] IS NULL AND [TipoSeguro] IS NULL) OR " +
            "([Categoria] = 1 AND [PrecoSeguroId] IS NOT NULL AND [PrecoSeguroId] = [OrigemId] AND [TaxaServicoId] IS NULL AND [TipoSeguro] IS NOT NULL AND [TipoSeguro] IN (0,1) AND [Modalidade] = 1))"));

        var estimativa = Imutavel<DemonstrativoEstimado>(builder, "DemonstrativosEstimados");
        revisao.HasOne(r => r.Estimativa).WithOne().HasForeignKey<DemonstrativoEstimado>(e => new { e.EmpresaId, e.RevisaoId })
            .HasPrincipalKey<RevisaoContrato>(r => new { r.EmpresaId, r.Id }).OnDelete(DeleteBehavior.NoAction);
        estimativa.Property(e => e.VersaoRegra).HasMaxLength(64).IsRequired();
        estimativa.Property(e => e.TermoDiariasBruto).HasPrecision(28, 9);
        estimativa.Property(e => e.TermoKmBruto).HasPrecision(28, 9);
        estimativa.Property(e => e.TotalConhecido).HasPrecision(19, 2);
        estimativa.ToTable(t => t.HasCheckConstraint("CK_DemonstrativosEstimados_Dados",
            "[Diarias] > 0 AND [Quilometros] IS NULL AND ([FranquiaTotalKm] IS NULL OR [FranquiaTotalKm] >= 0) AND [RetornoPrevistoUtc] > [SaidaUtc] AND [CalculadoEmUtc] >= [SaidaUtc] AND [TermoDiariasBruto] >= 0 AND [TermoKmBruto] = 0 AND [TotalConhecido] >= 0 AND [CombustivelPendente] = 1 AND [AtrasoPendente] = 1"));
        var componente = Imutavel<ComponenteEstimativa>(builder, "ComponentesEstimativa");
        estimativa.HasMany(e => e.Componentes).WithOne().HasForeignKey(c => new { c.EmpresaId, c.DemonstrativoId })
            .HasPrincipalKey(e => new { e.EmpresaId, e.Id }).OnDelete(DeleteBehavior.NoAction);
        estimativa.Navigation(e => e.Componentes).HasField("_componentes").UsePropertyAccessMode(PropertyAccessMode.Field);
        componente.HasIndex(c => new { c.EmpresaId, c.DemonstrativoId, c.Ordem }).IsUnique();
        componente.Property(c => c.Nome).HasMaxLength(200).IsRequired();
        componente.Property(c => c.PrecoUnitario).HasPrecision(19, 6);
        componente.Property(c => c.ValorBruto).HasPrecision(28, 9);
        componente.Property(c => c.ValorArredondado).HasPrecision(19, 2);
        componente.ToTable(t => t.HasCheckConstraint("CK_ComponentesEstimativa_Dados",
            "[Ordem] >= 0 AND [Multiplicador] > 0 AND [PrecoUnitario] >= 0 AND [ValorBruto] >= 0 AND [ValorArredondado] >= 0 AND " +
            "(([Categoria] IS NULL AND [TipoSeguro] IS NULL AND [Ordem] = 0) OR ([Categoria] = 0 AND [TipoSeguro] IS NULL) OR ([Categoria] = 1 AND [TipoSeguro] IS NOT NULL AND [TipoSeguro] IN (0,1)))"));

        var comando = Imutavel<ComandoIdempotente>(builder, "ComandosIdempotentes");
        comando.Property(c => c.Operacao).HasMaxLength(64).IsRequired();
        comando.Property(c => c.HashComando).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        comando.Property(c => c.VersaoOriginal).HasField("_versaoOriginal")
            .HasMaxLength(8).IsFixedLength().IsRequired().ValueGeneratedNever();
        comando.HasIndex(c => new { c.EmpresaId, c.ContaId, c.Operacao, c.Chave }).IsUnique();
        comando.HasOne<UsuarioConta>().WithMany().HasForeignKey(c => new { c.EmpresaId, c.ContaId })
            .HasPrincipalKey(u => new { u.EmpresaId, u.Id }).OnDelete(DeleteBehavior.NoAction);
        comando.HasOne<RevisaoContrato>().WithMany().HasForeignKey(c => new { c.EmpresaId, c.RevisaoId })
            .HasPrincipalKey(r => new { r.EmpresaId, r.Id }).OnDelete(DeleteBehavior.NoAction);
        comando.HasOne<DemonstrativoFinal>().WithMany().HasForeignKey(c => new { c.EmpresaId, c.RevisaoId, c.DemonstrativoFinalId })
            .HasPrincipalKey(f => new { f.EmpresaId, f.RevisaoId, f.Id }).OnDelete(DeleteBehavior.NoAction);
        comando.ToTable(t => t.HasCheckConstraint("CK_ComandosIdempotentes_Dados",
            "(([Operacao] = N'criar-aluguel' AND [DemonstrativoFinalId] IS NULL) OR ([Operacao] = N'devolver-aluguel' AND [DemonstrativoFinalId] IS NOT NULL)) AND LEN([HashComando]) = 64 AND [HashComando] NOT LIKE '%[^0-9A-F]%' COLLATE Latin1_General_100_BIN2 AND DATALENGTH([VersaoOriginal]) = 8"));

        var final = Imutavel<DemonstrativoFinal>(builder, "DemonstrativosFinais");
        final.HasAlternateKey(f => new { f.EmpresaId, f.RevisaoId, f.Id });
        aluguel.HasOne(a => a.Devolucao).WithOne().HasForeignKey<DemonstrativoFinal>(f => new { f.EmpresaId, f.AluguelId })
            .HasPrincipalKey<Aluguel>(a => new { a.EmpresaId, a.Id }).OnDelete(DeleteBehavior.NoAction);
        final.HasOne<RevisaoContrato>().WithMany().HasForeignKey(f => new { f.EmpresaId, f.AluguelId, f.RevisaoId })
            .HasPrincipalKey(r => new { r.EmpresaId, r.AluguelId, r.Id }).OnDelete(DeleteBehavior.NoAction);
        final.HasOne<PrecoCombustivel>().WithMany().HasForeignKey(f => new { f.EmpresaId, f.PrecoCombustivelId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id }).OnDelete(DeleteBehavior.NoAction);
        final.HasOne<Plano>().WithMany().HasForeignKey(f => new { f.EmpresaId, f.PlanoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id }).OnDelete(DeleteBehavior.NoAction);
        final.Property(f => f.VersaoRegra).HasMaxLength(64).IsRequired();
        final.Property(f => f.LitrosRestantes).HasPrecision(12, 3);
        final.Property(f => f.CapacidadeLitros).HasPrecision(12, 3);
        final.Property(f => f.PrecoCombustivel).HasPrecision(19, 6);
        final.Property(f => f.TermoDiariasBruto).HasPrecision(28, 9);
        final.Property(f => f.TermoKmBruto).HasPrecision(28, 9);
        final.Property(f => f.TotalFinal).HasPrecision(19, 2);
        final.Property(f => f.Caucao).HasPrecision(19, 2);
        final.ComplexProperty(f => f.Plano, p =>
        {
            p.IsRequired();
            p.Property(s => s.PrecoDiario).HasPrecision(19, 6);
            p.Property(s => s.PrecoKm).HasPrecision(19, 6);
            p.Property(s => s.PrecoKmExcedente).HasPrecision(19, 6);
        });
        final.ToTable(t =>
        {
            t.HasCheckConstraint("CK_DemonstrativosFinais_Dados",
                "[RetornoPrevistoUtc] > [SaidaUtc] AND [RetornoRealUtc] >= [SaidaUtc] AND [HodometroInicial] >= 0 AND [HodometroFinal] >= [HodometroInicial] AND [Quilometros] = [HodometroFinal] - [HodometroInicial] AND " +
                "[LitrosRestantes] >= 0 AND [CapacidadeLitros] > 0 AND [LitrosRestantes] <= [CapacidadeLitros] AND [TipoCombustivel] IN (0,1,2,3) AND [PrecoCombustivel] >= 0 AND [InicioVigenciaCombustivelUtc] <= [RetornoRealUtc] AND " +
                "[Diarias] >= 1 AND ([FranquiaTotalKm] IS NULL OR [FranquiaTotalKm] >= 0) AND ([ExcedenteKm] IS NULL OR [ExcedenteKm] >= 0) AND [TermoDiariasBruto] >= 0 AND [TermoKmBruto] >= 0 AND [TotalFinal] >= 0 AND [Caucao] = 1000 AND [CalculadoEmUtc] >= [SaidaUtc] AND [RegistradoEmUtc] >= [CalculadoEmUtc]");
            t.HasCheckConstraint("CK_DemonstrativosFinais_Plano", ParametrosPlano);
        });

        var itemDevolucao = Imutavel<ItemDevolucao>(builder, "ItensDevolucao");
        final.HasMany(f => f.Itens).WithOne().HasForeignKey(i => new { i.EmpresaId, i.DemonstrativoFinalId })
            .HasPrincipalKey(f => new { f.EmpresaId, f.Id }).OnDelete(DeleteBehavior.NoAction);
        final.Navigation(f => f.Itens).HasField("_itens").UsePropertyAccessMode(PropertyAccessMode.Field);
        itemDevolucao.HasOne<TaxaServico>().WithMany().HasForeignKey(i => new { i.EmpresaId, i.TaxaServicoId })
            .HasPrincipalKey(t => new { t.EmpresaId, t.Id }).OnDelete(DeleteBehavior.NoAction);
        itemDevolucao.HasIndex(i => new { i.EmpresaId, i.DemonstrativoFinalId, i.TaxaServicoId }).IsUnique();
        itemDevolucao.Property(i => i.Nome).HasMaxLength(200).IsRequired();
        itemDevolucao.Property(i => i.PrecoUnitario).HasPrecision(19, 6);
        itemDevolucao.Property(i => i.VersaoTaxa).HasField("_versaoTaxa").HasMaxLength(8).IsFixedLength().IsRequired().ValueGeneratedNever();
        itemDevolucao.ToTable(t => t.HasCheckConstraint("CK_ItensDevolucao_Dados",
            "[PrecoUnitario] >= 0 AND [Modalidade] IN (0,1) AND [OrigemInclusao] = 2 AND DATALENGTH([VersaoTaxa]) = 8"));

        var componenteFinal = Imutavel<ComponenteFinal>(builder, "ComponentesFinal");
        final.HasMany(f => f.Componentes).WithOne().HasForeignKey(c => new { c.EmpresaId, c.DemonstrativoFinalId })
            .HasPrincipalKey(f => new { f.EmpresaId, f.Id }).OnDelete(DeleteBehavior.NoAction);
        final.Navigation(f => f.Componentes).HasField("_componentes").UsePropertyAccessMode(PropertyAccessMode.Field);
        componenteFinal.HasIndex(c => new { c.EmpresaId, c.DemonstrativoFinalId, c.Ordem }).IsUnique();
        componenteFinal.Property(c => c.Nome).HasMaxLength(200).IsRequired();
        componenteFinal.Property(c => c.Quantidade).HasPrecision(12, 3);
        componenteFinal.Property(c => c.PrecoUnitario).HasPrecision(19, 6);
        componenteFinal.Property(c => c.ValorBruto).HasPrecision(28, 9);
        componenteFinal.Property(c => c.ValorArredondado).HasPrecision(19, 2);
        componenteFinal.ToTable(t => t.HasCheckConstraint("CK_ComponentesFinal_Dados",
            "[Ordem] >= 0 AND [Quantidade] >= 0 AND [PrecoUnitario] >= 0 AND [ValorBruto] >= 0 AND [ValorArredondado] >= 0 AND " +
            "(([Categoria] = 0 AND [OrigemId] IS NOT NULL AND [TipoSeguro] IS NULL AND [Modalidade] IS NULL AND [OrigemInclusao] IS NULL AND [Ordem] = 0) OR " +
            "([Categoria] = 1 AND [OrigemId] IS NOT NULL AND [TipoSeguro] IS NULL AND [Modalidade] IS NOT NULL AND [Modalidade] IN (0,1) AND [OrigemInclusao] IS NOT NULL AND [OrigemInclusao] IN (0,1,2)) OR " +
            "([Categoria] = 2 AND [OrigemId] IS NOT NULL AND [TipoSeguro] IS NOT NULL AND [TipoSeguro] IN (0,1) AND [Modalidade] = 1 AND [OrigemInclusao] IS NOT NULL AND [OrigemInclusao] IN (0,1)) OR " +
            "([Categoria] = 3 AND [OrigemId] IS NOT NULL AND [TipoSeguro] IS NULL AND [Modalidade] IS NULL AND [OrigemInclusao] IS NULL) OR " +
            "([Categoria] = 4 AND [OrigemId] IS NULL AND [TipoSeguro] IS NULL AND [Modalidade] IS NULL AND [OrigemInclusao] IS NULL))"));
    }

    private static EntityTypeBuilder<T> Imutavel<T>(ModelBuilder builder, string tabela) where T : class, IEntidadeEmpresa
    {
        var entidade = builder.Entity<T>();
        entidade.ToTable(tabela);
        entidade.HasKey("Id");
        entidade.Property<Guid>("Id").ValueGeneratedNever();
        entidade.HasAlternateKey("EmpresaId", "Id");
        entidade.HasOne<Empresa>().WithMany().HasForeignKey(e => e.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        return entidade;
    }
}
