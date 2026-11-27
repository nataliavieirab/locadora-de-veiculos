using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.Transversal;
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
    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<Plano> Planos => Set<Plano>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<VinculoCliente> VinculosClientes => Set<VinculoCliente>();
    public DbSet<Condutor> Condutores => Set<Condutor>();
    public DbSet<TaxaServico> TaxasServicos => Set<TaxaServico>();
    public DbSet<PrecoSeguro> PrecosSeguros => Set<PrecoSeguro>();
    public DbSet<PrecoCombustivel> PrecosCombustiveis => Set<PrecoCombustivel>();
    public DbSet<AuditoriaCadastro> AuditoriasCadastro => Set<AuditoriaCadastro>();
    public DbSet<Aluguel> Alugueis => Set<Aluguel>();
    public DbSet<RevisaoContrato> RevisoesContrato => Set<RevisaoContrato>();
    public DbSet<ItemContratado> ItensContratados => Set<ItemContratado>();
    public DbSet<DemonstrativoEstimado> DemonstrativosEstimados => Set<DemonstrativoEstimado>();
    public DbSet<ComponenteEstimativa> ComponentesEstimativa => Set<ComponenteEstimativa>();
    public DbSet<ComandoIdempotente> ComandosIdempotentes => Set<ComandoIdempotente>();
    public DbSet<DemonstrativoFinal> DemonstrativosFinais => Set<DemonstrativoFinal>();
    public DbSet<ItemDevolucao> ItensDevolucao => Set<ItemDevolucao>();
    public DbSet<ComponenteFinal> ComponentesFinal => Set<ComponenteFinal>();
    public DbSet<FotoObjeto> FotosObjetos => Set<FotoObjeto>();
    public DbSet<TrabalhoObjeto> TrabalhosObjetos => Set<TrabalhoObjeto>();

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

        MapeamentosCadastros.Aplicar(builder);
        builder.Entity<Grupo>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId && item.ExcluidoEmUtc == null);
        builder.Entity<Plano>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId && item.ExcluidoEmUtc == null);
        builder.Entity<Veiculo>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId && item.ExcluidoEmUtc == null);
        builder.Entity<Cliente>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId && item.ExcluidoEmUtc == null);
        builder.Entity<VinculoCliente>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId && item.ExcluidoEmUtc == null);
        builder.Entity<Condutor>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId && item.ExcluidoEmUtc == null);
        builder.Entity<TaxaServico>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId && item.ExcluidoEmUtc == null);
        builder.Entity<PrecoSeguro>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<PrecoCombustivel>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<AuditoriaCadastro>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        MapeamentosLocacoes.Aplicar(builder);
        builder.Entity<Aluguel>()
            .HasQueryFilter("Empresa", item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId)
            .HasQueryFilter("ExclusaoLogicaAluguel", item => item.ExcluidoEmUtc == null);
        builder.Entity<RevisaoContrato>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<ItemContratado>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<DemonstrativoEstimado>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<ComponenteEstimativa>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<ComandoIdempotente>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<DemonstrativoFinal>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<ItemDevolucao>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<ComponenteFinal>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        MapeamentosFotos.Aplicar(builder);
        builder.Entity<FotoObjeto>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
        builder.Entity<TrabalhoObjeto>().HasQueryFilter(item => EmpresaAtualId != null && item.EmpresaId == EmpresaAtualId);
    }
}
