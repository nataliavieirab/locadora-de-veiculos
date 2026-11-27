using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Locadora.Infrastructure.Persistencia;

internal static class MapeamentosFotos
{
    public static void Aplicar(ModelBuilder builder)
    {
        var foto = Mutavel(builder.Entity<FotoObjeto>(), "FotosObjetos");
        foto.HasAlternateKey(f => new { f.EmpresaId, f.VeiculoId, f.Id });
        foto.Property(f => f.Mime).HasMaxLength(32).IsRequired();
        foto.Property(f => f.Sha256).HasMaxLength(32).IsRequired();
        foto.Property(f => f.VersaoBase).HasMaxLength(8).IsRequired();
        foto.Property(f => f.Provedor).HasConversion<string>().HasMaxLength(16).IsRequired();
        foto.Property(f => f.Bucket).HasMaxLength(63).IsRequired();
        foto.Property(f => f.Chave).HasMaxLength(256).IsRequired();
        foto.Property(f => f.Caminho).HasMaxLength(128).IsRequired();
        foto.Property(f => f.Correlacao).HasMaxLength(64).IsRequired();
        foto.HasOne<Veiculo>().WithMany().HasForeignKey(f => new { f.EmpresaId, f.VeiculoId })
            .HasPrincipalKey(v => new { v.EmpresaId, v.Id }).OnDelete(DeleteBehavior.NoAction);
        foto.HasOne<UsuarioConta>().WithMany().HasForeignKey(f => new { f.EmpresaId, f.AtorId })
            .HasPrincipalKey(u => new { u.EmpresaId, u.Id }).OnDelete(DeleteBehavior.NoAction);
        foto.HasIndex(f => new { f.EmpresaId, f.VeiculoId }).IsUnique().HasFilter("[Estado] = 1");
        foto.HasIndex(f => new { f.Provedor, f.Bucket, f.Chave }).IsUnique();
        foto.HasIndex(f => new { f.Estado, f.ExcluirAposUtc });
        foto.ToTable(t =>
        {
            t.HasCheckConstraint("CK_FotosObjetos_Estado", "[Estado] IN (0,1,2,3)");
            t.HasCheckConstraint("CK_FotosObjetos_Retencao",
                "([Estado] <> 2 AND [RemovidaEmUtc] IS NULL AND [ExcluirAposUtc] IS NULL AND [RetencaoLegada]=0) OR " +
                "([Estado]=2 AND [ExcluirAposUtc] IS NOT NULL AND " +
                "(([RetencaoLegada]=1 AND [RemovidaEmUtc] IS NULL) OR " +
                "([RetencaoLegada]=0 AND [RemovidaEmUtc] IS NOT NULL AND [ExcluirAposUtc]=DATEADD(day,30,[RemovidaEmUtc]))))");
            t.HasCheckConstraint("CK_FotosObjetos_Imagem",
                "[Mime] IN ('image/jpeg','image/png') AND [TamanhoBytes] BETWEEN 1 AND 5242880 AND " +
                "[Largura] > 0 AND [Altura] > 0 AND CAST([Largura] AS bigint)*[Altura] <= 16000000 AND " +
                "DATALENGTH([Sha256]) = 32 AND DATALENGTH([VersaoBase]) = 8 AND [Provedor] IN ('Local','R2')");
        });
        builder.Entity<Veiculo>().HasOne<FotoObjeto>().WithMany()
            .HasForeignKey(v => new { v.EmpresaId, v.Id, v.FotoAtivaId })
            .HasPrincipalKey(f => new { f.EmpresaId, f.VeiculoId, f.Id }).OnDelete(DeleteBehavior.NoAction);

        var trabalho = Mutavel(builder.Entity<TrabalhoObjeto>(), "TrabalhosObjetos");
        trabalho.HasOne<FotoObjeto>().WithMany().HasForeignKey(t => new { t.EmpresaId, t.FotoObjetoId })
            .HasPrincipalKey(f => new { f.EmpresaId, f.Id }).OnDelete(DeleteBehavior.NoAction);
        trabalho.Property(t => t.CodigoFalha).HasMaxLength(64);
        trabalho.Ignore(t => t.EnvioConfirmado);
        trabalho.HasIndex(t => new { t.EmpresaId, t.FotoObjetoId }).IsUnique();
        trabalho.HasIndex(t => new { t.Estado, t.ProximoProcessamentoUtc });
        trabalho.ToTable(t =>
        {
            t.HasCheckConstraint("CK_TrabalhosObjetos_Estado",
                "[Tipo] IN (0,1) AND [Estado] IN (0,1,2,4) AND [Tentativas] >= 0 AND " +
                "([Tipo] = 1 OR [Estado] <> 4)");
            t.HasCheckConstraint("CK_TrabalhosObjetos_Posse",
                "([Estado] = 1 AND [TokenPosse] IS NOT NULL AND [LeaseAteUtc] IS NOT NULL) OR " +
                "([Estado] <> 1 AND [TokenPosse] IS NULL AND [LeaseAteUtc] IS NULL)");
        });
    }

    private static EntityTypeBuilder<T> Mutavel<T>(EntityTypeBuilder<T> entidade, string tabela) where T : EntidadeCadastro
    {
        entidade.HasBaseType((Type?)null);
        entidade.ToTable(tabela, t => t.HasCheckConstraint($"CK_{tabela}_Instantes", "[AlteradoEmUtc] >= [CriadoEmUtc]"));
        entidade.HasKey(e => e.Id);
        entidade.HasAlternateKey(e => new { e.EmpresaId, e.Id });
        entidade.Property(e => e.RowVersion).IsRowVersion();
        entidade.HasOne<Empresa>().WithMany().HasForeignKey(e => e.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        return entidade;
    }
}
