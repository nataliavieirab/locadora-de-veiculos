using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Locadora.Infrastructure.Modulos.Transversal;

public sealed class GuardaEscritaEmpresa : SaveChangesInterceptor
{
    private readonly ContextoEmpresa _contexto;

    public GuardaEscritaEmpresa(ContextoEmpresa contexto)
    {
        _contexto = contexto;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Validar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Validar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Validar(DbContext? contexto)
    {
        if (contexto is null)
        {
            throw new EmpresaDivergenteException();
        }

        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entrada.Entity is Empresa)
            {
                if (entrada.State != EntityState.Added || !_contexto.PermiteCriarEmpresa)
                {
                    throw new EmpresaDivergenteException();
                }

                continue;
            }

            if (entrada.Entity is PrecoCombustivel or AuditoriaCadastro or RevisaoContrato or ItemContratado
                or DemonstrativoEstimado or ComponenteEstimativa or ComandoIdempotente
                && entrada.State != EntityState.Added)
            {
                throw new InvalidOperationException("Registros históricos não podem ser alterados ou removidos.");
            }

            if (entrada.Entity is Aluguel && (entrada.State == EntityState.Deleted
                || entrada.State == EntityState.Modified && entrada.Property(nameof(Aluguel.SaidaUtc)).IsModified))
            {
                throw new InvalidOperationException("O aluguel não permite remoção nem alteração da saída original.");
            }

            if (entrada.Entity is not IEntidadeEmpresa entidade)
            {
                continue;
            }

            if (_contexto.EmpresaId is null || entidade.EmpresaId != _contexto.EmpresaId)
            {
                throw new EmpresaDivergenteException();
            }
        }
    }
}
