using Locadora.Domain.Modulos.Acesso;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.Transversal;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Persistencia;

public sealed class LocadoraDbContext : IdentityUserContext<UsuarioConta, Guid>
{
    private readonly ContextoEmpresa _contexto;

    public LocadoraDbContext(DbContextOptions<LocadoraDbContext> options, ContextoEmpresa contexto)
        : base(options)
    {
        _contexto = contexto;
    }

    public Guid? EmpresaAtualId => _contexto.EmpresaId;

    public DbSet<Empresa> Empresas => Set<Empresa>();

    public DbSet<AuditoriaAcesso> AuditoriasAcesso => Set<AuditoriaAcesso>();

    public DbSet<SessaoAutenticacao> SessoesAutenticacao => Set<SessaoAutenticacao>();

    public DbSet<RefreshSessao> RefreshesSessao => Set<RefreshSessao>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Empresa>(entidade =>
        {
            entidade.ToTable("Empresas");
            entidade.HasKey(empresa => empresa.Id);
        });

        builder.Entity<AuditoriaAcesso>(entidade =>
        {
            entidade.ToTable("AuditoriasAcesso");
            entidade.HasKey(auditoria => auditoria.Id);
            entidade.Property(auditoria => auditoria.Acao).HasMaxLength(64).IsRequired();
            entidade.HasQueryFilter(auditoria => EmpresaAtualId != null && auditoria.EmpresaId == EmpresaAtualId);
        });

        builder.Entity<SessaoAutenticacao>(entidade =>
        {
            entidade.ToTable("SessoesAutenticacao");
            entidade.HasKey(sessao => sessao.Id);
            entidade.HasIndex(sessao => sessao.FamiliaId);
            entidade.HasQueryFilter(sessao => EmpresaAtualId != null && sessao.EmpresaId == EmpresaAtualId);
        });

        builder.Entity<RefreshSessao>(entidade =>
        {
            entidade.ToTable("RefreshesSessao");
            entidade.HasKey(refresh => refresh.Id);
            entidade.Property(refresh => refresh.Hash).HasMaxLength(64).IsRequired();
            entidade.HasIndex(refresh => refresh.Hash).IsUnique();
            entidade.HasIndex(refresh => refresh.FamiliaId);
            entidade.HasQueryFilter(refresh => EmpresaAtualId != null && refresh.EmpresaId == EmpresaAtualId);
        });

        builder.Entity<UsuarioConta>(entidade =>
        {
            entidade.Property(usuario => usuario.EmpresaId).IsRequired();
            entidade.HasIndex(usuario => usuario.EmpresaId).IsUnique();
        });

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("AspNetUserClaims");
    }
}
