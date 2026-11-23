using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Locadora.Infrastructure.Persistencia;

internal static class MapeamentosCadastros
{
    public static void Aplicar(ModelBuilder builder)
    {
        builder.HasSequence<long>("OrdemVigenciaCombustivel");

        var grupo = Cadastro(builder.Entity<Grupo>(), "Grupos");
        grupo.Property(item => item.Nome).HasMaxLength(200).IsRequired();

        var plano = Cadastro(builder.Entity<Plano>(), "Planos");
        plano.Property(item => item.PrecoDiario).HasPrecision(19, 6);
        plano.Property(item => item.PrecoKm).HasPrecision(19, 6);
        plano.Property(item => item.PrecoKmExcedente).HasPrecision(19, 6);
        plano.HasIndex(item => new { item.EmpresaId, item.GrupoId, item.Modalidade })
            .IsUnique().HasFilter("[ExcluidoEmUtc] IS NULL");
        plano.HasOne<Grupo>().WithMany().HasForeignKey(item => new { item.EmpresaId, item.GrupoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.NoAction);
        plano.ToTable(tabela => tabela.HasCheckConstraint("CK_Planos_Parametros",
            "[PrecoDiario] >= 0 AND (([Modalidade] = 0 AND [PrecoKm] IS NOT NULL AND [PrecoKm] >= 0 AND [FranquiaDiariaKm] IS NULL AND [PrecoKmExcedente] IS NULL) OR " +
            "([Modalidade] = 1 AND [PrecoKm] IS NULL AND [FranquiaDiariaKm] IS NOT NULL AND [FranquiaDiariaKm] >= 0 AND [PrecoKmExcedente] IS NOT NULL AND [PrecoKmExcedente] >= 0) OR " +
            "([Modalidade] = 2 AND [PrecoKm] IS NULL AND [FranquiaDiariaKm] IS NULL AND [PrecoKmExcedente] IS NULL))"));

        var veiculo = Cadastro(builder.Entity<Veiculo>(), "Veiculos");
        veiculo.Property(item => item.Placa).HasMaxLength(16).IsRequired();
        veiculo.Property(item => item.PlacaNormalizada).HasMaxLength(16).IsRequired();
        veiculo.Property(item => item.Marca).HasMaxLength(100).IsRequired();
        veiculo.Property(item => item.Cor).HasMaxLength(100).IsRequired();
        veiculo.Property(item => item.Modelo).HasMaxLength(100).IsRequired();
        veiculo.Property(item => item.CapacidadeLitros).HasPrecision(12, 3);
        veiculo.HasIndex(item => new { item.EmpresaId, item.PlacaNormalizada }).IsUnique().HasFilter("[ExcluidoEmUtc] IS NULL");
        veiculo.HasIndex(item => new { item.EmpresaId, item.GrupoId, item.Id });
        veiculo.HasOne<Grupo>().WithMany().HasForeignKey(item => new { item.EmpresaId, item.GrupoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.NoAction);
        veiculo.ToTable(tabela => tabela.HasCheckConstraint("CK_Veiculos_Dados",
            "[CapacidadeLitros] > 0 AND [Ano] > 0 AND [Combustivel] IN (0,1,2,3)"));

        var cliente = Cadastro(builder.Entity<Cliente>(), "Clientes");
        cliente.Property(item => item.Nome).HasMaxLength(200).IsRequired();
        cliente.Property(item => item.Cpf).HasMaxLength(64);
        cliente.Property(item => item.CpfNormalizado).HasMaxLength(11);
        cliente.Property(item => item.Cnpj).HasMaxLength(64);
        cliente.Property(item => item.CnpjNormalizado).HasMaxLength(14);
        cliente.Property(item => item.Rg).HasMaxLength(64);
        cliente.Property(item => item.Cnh).HasMaxLength(64);
        cliente.Property(item => item.CnhNormalizada).HasMaxLength(64);
        cliente.Property(item => item.Telefone).HasMaxLength(32).IsRequired();
        cliente.Property(item => item.Email).HasMaxLength(254);
        cliente.ComplexProperty(item => item.Endereco, endereco =>
        {
            endereco.IsRequired();
            endereco.Property(item => item.Estado).HasMaxLength(100).IsRequired();
            endereco.Property(item => item.Cidade).HasMaxLength(100).IsRequired();
            endereco.Property(item => item.Bairro).HasMaxLength(100).IsRequired();
            endereco.Property(item => item.Rua).HasMaxLength(200).IsRequired();
            endereco.Property(item => item.Numero).HasMaxLength(200).IsRequired();
        });
        cliente.HasIndex(item => new { item.EmpresaId, item.CpfNormalizado }).IsUnique()
            .HasFilter("[ExcluidoEmUtc] IS NULL AND [CpfNormalizado] IS NOT NULL");
        cliente.HasIndex(item => new { item.EmpresaId, item.CnpjNormalizado }).IsUnique()
            .HasFilter("[ExcluidoEmUtc] IS NULL AND [CnpjNormalizado] IS NOT NULL");
        cliente.ToTable(tabela => tabela.HasCheckConstraint("CK_Clientes_Tipo",
            "([Tipo] = 0 AND [Cpf] IS NOT NULL AND [CpfNormalizado] IS NOT NULL AND [Rg] IS NOT NULL AND [Cnh] IS NOT NULL AND [CnhNormalizada] IS NOT NULL AND [Cnpj] IS NULL AND [CnpjNormalizado] IS NULL) OR " +
            "([Tipo] = 1 AND [Cnpj] IS NOT NULL AND [CnpjNormalizado] IS NOT NULL AND [Cpf] IS NULL AND [CpfNormalizado] IS NULL AND [Rg] IS NULL AND [Cnh] IS NULL AND [CnhNormalizada] IS NULL)"));

        var vinculo = Cadastro(builder.Entity<VinculoCliente>(), "VinculosClientes");
        vinculo.HasIndex(item => new { item.EmpresaId, item.ClientePfId, item.ClientePjId })
            .IsUnique().HasFilter("[ExcluidoEmUtc] IS NULL");
        vinculo.HasOne<Cliente>().WithMany().HasForeignKey(item => new { item.EmpresaId, item.ClientePfId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.NoAction);
        vinculo.HasOne<Cliente>().WithMany().HasForeignKey(item => new { item.EmpresaId, item.ClientePjId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.NoAction);
        vinculo.ToTable(tabela => tabela.HasCheckConstraint("CK_VinculosClientes_Distintos", "[ClientePfId] <> [ClientePjId]"));

        var condutor = Cadastro(builder.Entity<Condutor>(), "Condutores");
        condutor.Property(item => item.Nome).HasMaxLength(200).IsRequired();
        condutor.Property(item => item.Email).HasMaxLength(254).IsRequired();
        condutor.Property(item => item.Cpf).HasMaxLength(64).IsRequired();
        condutor.Property(item => item.CpfNormalizado).HasMaxLength(11).IsRequired();
        condutor.Property(item => item.Cnh).HasMaxLength(64).IsRequired();
        condutor.Property(item => item.CnhNormalizada).HasMaxLength(64).IsRequired();
        condutor.Property(item => item.ValidadeCnh).HasColumnType("date");
        condutor.Property(item => item.Telefone).HasMaxLength(32).IsRequired();
        condutor.HasIndex(item => new { item.EmpresaId, item.CpfNormalizado }).IsUnique().HasFilter("[ExcluidoEmUtc] IS NULL");
        condutor.HasOne<Cliente>().WithMany().HasForeignKey(item => new { item.EmpresaId, item.ClienteId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.NoAction);

        var taxa = Cadastro(builder.Entity<TaxaServico>(), "TaxasServicos");
        taxa.Property(item => item.Nome).HasMaxLength(200).IsRequired();
        taxa.Property(item => item.Preco).HasPrecision(19, 6);
        taxa.ToTable(tabela => tabela.HasCheckConstraint("CK_TaxasServicos_PrecoModalidade", "[Preco] >= 0 AND [Modalidade] IN (0,1)"));

        var seguro = Mutavel(builder.Entity<PrecoSeguro>(), "PrecosSeguros");
        seguro.Property(item => item.PrecoDiario).HasPrecision(19, 6);
        seguro.HasIndex(item => new { item.EmpresaId, item.Tipo }).IsUnique();
        seguro.ToTable(tabela => tabela.HasCheckConstraint("CK_PrecosSeguros_PrecoTipo", "[PrecoDiario] >= 0 AND [Tipo] IN (0,1)"));

        var combustivel = builder.Entity<PrecoCombustivel>();
        combustivel.ToTable("PrecosCombustiveis", tabela => tabela.HasCheckConstraint(
            "CK_PrecosCombustiveis_PrecoTipo", "[PrecoUnitario] >= 0 AND [Tipo] IN (0,1,2,3)"));
        combustivel.HasKey(item => item.Id);
        combustivel.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        combustivel.HasOne<Empresa>().WithMany().HasForeignKey(item => item.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        combustivel.Property(item => item.PrecoUnitario).HasPrecision(19, 6);
        combustivel.Property(item => item.Ordem).HasDefaultValueSql("NEXT VALUE FOR [OrdemVigenciaCombustivel]").ValueGeneratedOnAdd();
        combustivel.HasIndex(item => new { item.EmpresaId, item.Tipo, item.InicioVigenciaUtc, item.Ordem, item.Id });

        var auditoria = builder.Entity<AuditoriaCadastro>();
        auditoria.ToTable("AuditoriasCadastro");
        auditoria.HasKey(item => item.Id);
        auditoria.HasOne<Empresa>().WithMany().HasForeignKey(item => item.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        auditoria.HasOne<Infrastructure.Modulos.Acesso.Registro.UsuarioConta>().WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.ContaId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.NoAction);
        auditoria.Property(item => item.CamposAlterados).HasMaxLength(1000).IsRequired();
        auditoria.HasIndex(item => new { item.EmpresaId, item.Entidade, item.EntidadeId, item.MomentoUtc });
    }

    private static EntityTypeBuilder<T> Mutavel<T>(EntityTypeBuilder<T> entidade, string tabela) where T : EntidadeCadastro
    {
        entidade.HasBaseType((Type?)null);
        entidade.ToTable(tabela, opcoes => opcoes.HasCheckConstraint($"CK_{tabela}_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]"));
        entidade.HasKey(item => item.Id);
        entidade.HasAlternateKey(item => new { item.EmpresaId, item.Id });
        entidade.Property(item => item.RowVersion).IsRowVersion();
        entidade.HasOne<Empresa>().WithMany().HasForeignKey(item => item.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        return entidade;
    }

    private static EntityTypeBuilder<T> Cadastro<T>(EntityTypeBuilder<T> entidade, string tabela) where T : CadastroExcluivel
    {
        Mutavel(entidade, tabela);
        entidade.ToTable(opcoes => opcoes.HasCheckConstraint($"CK_{tabela}_Exclusao",
            "[ExcluidoEmUtc] IS NULL OR [ExcluidoEmUtc] = [AlteradoEmUtc]"));
        return entidade;
    }
}
