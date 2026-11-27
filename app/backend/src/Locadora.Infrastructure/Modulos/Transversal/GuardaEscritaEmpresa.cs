using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Fotos;
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

            if (entrada.Entity is IEntidadeEmpresa empresa
                && (_contexto.EmpresaId is null || empresa.EmpresaId != _contexto.EmpresaId))
                throw new EmpresaDivergenteException();

            if (entrada.Entity is PrecoCombustivel or AuditoriaCadastro or RevisaoContrato or ItemContratado
                or DemonstrativoEstimado or ComponenteEstimativa or ComandoIdempotente
                or DemonstrativoFinal or ItemDevolucao or ComponenteFinal
                && entrada.State != EntityState.Added)
            {
                throw new InvalidOperationException("Registros históricos não podem ser alterados ou removidos.");
            }

            if (entrada.Entity is FotoObjeto or TrabalhoObjeto)
            {
                if (entrada.State == EntityState.Deleted || contexto.Database.CurrentTransaction is null)
                    throw new InvalidOperationException("Fotos e trabalhos exigem transação e não admitem exclusão física do registro.");
                if (entrada.Entity is FotoObjeto && entrada.State == EntityState.Modified)
                {
                    if (entrada.Properties.Any(p => p.IsModified && p.Metadata.Name is not
                        (nameof(FotoObjeto.Estado) or nameof(FotoObjeto.AlteradoEmUtc)
                        or nameof(FotoObjeto.RemovidaEmUtc) or nameof(FotoObjeto.ExcluirAposUtc))))
                        throw new InvalidOperationException("Metadados e destino da foto são imutáveis.");
                    var estadoAnterior = (EstadoFoto)entrada.Property(nameof(FotoObjeto.Estado)).OriginalValue!;
                    var estadoAtual = ((FotoObjeto)entrada.Entity).Estado;
                    var foto = (FotoObjeto)entrada.Entity;
                    var mudouRetencao = entrada.Property(nameof(FotoObjeto.RemovidaEmUtc)).IsModified
                        || entrada.Property(nameof(FotoObjeto.ExcluirAposUtc)).IsModified;
                    if (mudouRetencao && (estadoAnterior != EstadoFoto.Ativa || estadoAtual != EstadoFoto.Inativa
                        || entrada.Property(nameof(FotoObjeto.RemovidaEmUtc)).OriginalValue is not null
                        || entrada.Property(nameof(FotoObjeto.ExcluirAposUtc)).OriginalValue is not null
                        || foto.RemovidaEmUtc != foto.AlteradoEmUtc
                        || foto.ExcluirAposUtc != foto.RemovidaEmUtc?.AddDays(30)))
                        throw new InvalidOperationException("O prazo de retenção só pode ser gravado na inativação.");
                    if (estadoAtual != estadoAnterior && !((estadoAnterior == EstadoFoto.Pendente
                        && estadoAtual is EstadoFoto.Ativa or EstadoFoto.Abandonada)
                        || (estadoAnterior == EstadoFoto.Ativa && estadoAtual == EstadoFoto.Inativa)))
                        throw new InvalidOperationException("Transição de foto inválida.");
                }
                if (entrada.Entity is TrabalhoObjeto && entrada.State == EntityState.Modified
                    && entrada.Properties.Any(p => p.IsModified && p.Metadata.Name is
                        (nameof(TrabalhoObjeto.FotoObjetoId)
                        or nameof(TrabalhoObjeto.EmpresaId) or nameof(TrabalhoObjeto.CriadoEmUtc))))
                    throw new InvalidOperationException("A origem do trabalho é imutável.");
                if (entrada.Entity is TrabalhoObjeto && entrada.State == EntityState.Modified
                    && entrada.Property(nameof(TrabalhoObjeto.EnvioConfirmadoEmUtc)).IsModified
                    && entrada.Property(nameof(TrabalhoObjeto.EnvioConfirmadoEmUtc)).OriginalValue is not null)
                    throw new InvalidOperationException("A confirmação de envio é imutável.");
                if (entrada.Entity is TrabalhoObjeto trabalho && entrada.State == EntityState.Modified
                    && entrada.Property(nameof(TrabalhoObjeto.Tipo)).IsModified
                    && (entrada.Property(nameof(TrabalhoObjeto.Tipo)).OriginalValue is not TipoTrabalhoObjeto.CompensarNuncaAtivo
                        || trabalho.Tipo != TipoTrabalhoObjeto.RemocaoRetida || trabalho.Estado != EstadoTrabalhoObjeto.Retido
                        || !contexto.ChangeTracker.Entries<FotoObjeto>().Any(f => f.Entity.Id == trabalho.FotoObjetoId
                            && f.Entity.EmpresaId == trabalho.EmpresaId && f.Entity.Estado is EstadoFoto.Ativa or EstadoFoto.Inativa)))
                    throw new InvalidOperationException("O trabalho só pode migrar de compensação para retenção de foto ativada.");
            }
            if (entrada.Entity is Veiculo && entrada.State == EntityState.Modified
                && entrada.Property(nameof(Veiculo.FotoAtivaId)).IsModified
                && contexto.Database.CurrentTransaction is null)
                throw new InvalidOperationException("A referência de foto exige transação de empresa.");

            if (entrada.Entity is Aluguel && (entrada.State == EntityState.Deleted
                || entrada.State == EntityState.Modified && entrada.Property(nameof(Aluguel.SaidaUtc)).IsModified))
            {
                throw new InvalidOperationException("O aluguel não permite remoção nem alteração da saída original.");
            }

            if (entrada.Entity is DemonstrativoFinal or ItemDevolucao or ComponenteFinal
                || entrada.Entity is AuditoriaCadastro { Acao: AcaoCadastro.Devolucao or AcaoCadastro.Ocultacao }
                || entrada.Entity is ComandoIdempotente { Operacao: "devolver-aluguel" })
            {
                if (contexto.Database.CurrentTransaction is null)
                    throw new InvalidOperationException("O fechamento e sua auditoria exigem transação de empresa.");
            }

            if (entrada.Entity is Aluguel aluguel && entrada.State == EntityState.Modified)
            {
                var original = entrada.Property(nameof(Aluguel.Estado)).OriginalValue;
                if (original is EstadoAluguel.Concluido)
                {
                    if (entrada.Properties.Any(p => p.IsModified && p.Metadata.Name is not
                        (nameof(Aluguel.ExcluidoEmUtc) or nameof(Aluguel.AlteradoEmUtc)))
                        || entrada.Property(nameof(Aluguel.ExcluidoEmUtc)).OriginalValue is not null)
                        throw new InvalidOperationException("Contrato concluído só admite a primeira ocultação.");
                }
                if (entrada.Property(nameof(Aluguel.ExcluidoEmUtc)).IsModified)
                {
                    if (contexto.Database.CurrentTransaction is null || aluguel.Estado != EstadoAluguel.Concluido
                        || aluguel.ExcluidoEmUtc is null || !contexto.ChangeTracker.Entries<AuditoriaCadastro>().Any(a =>
                            a.State == EntityState.Added && a.Entity.EmpresaId == aluguel.EmpresaId
                            && a.Entity.EntidadeId == aluguel.Id && a.Entity.Acao == AcaoCadastro.Ocultacao))
                        throw new InvalidOperationException("A ocultação exige transação e auditoria da empresa.");
                }
                if (original is EstadoAluguel.EmAberto && aluguel.Estado == EstadoAluguel.Concluido
                    && (contexto.Database.CurrentTransaction is null
                        || !contexto.ChangeTracker.Entries<DemonstrativoFinal>().Any(f => f.State == EntityState.Added
                            && f.Entity.EmpresaId == aluguel.EmpresaId && f.Entity.AluguelId == aluguel.Id)
                        || !contexto.ChangeTracker.Entries<AuditoriaCadastro>().Any(a => a.State == EntityState.Added
                            && a.Entity.EmpresaId == aluguel.EmpresaId && a.Entity.EntidadeId == aluguel.Id
                            && a.Entity.Acao == AcaoCadastro.Devolucao)))
                    throw new InvalidOperationException("A conclusão exige final e auditoria na mesma transação.");
            }

            if (entrada.Entity is DemonstrativoFinal final && !contexto.ChangeTracker.Entries<Aluguel>().Any(a =>
                a.Entity.EmpresaId == final.EmpresaId && a.Entity.Id == final.AluguelId
                && a.Entity.Estado == EstadoAluguel.Concluido && a.Entity.Devolucao?.Id == final.Id
                && a.Entity.RevisaoAtual.Id == final.RevisaoId))
                throw new InvalidOperationException("O final exige a transição da revisão corrente do aluguel.");

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
