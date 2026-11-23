using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Locadora.Infrastructure.Modulos.Transversal;

public sealed class UnidadeTrabalhoEmpresa(
    LocadoraDbContext banco,
    ContextoEmpresa contexto,
    ILogger<UnidadeTrabalhoEmpresa> logger) : IUnidadeTrabalhoEmpresa
{
    public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(comando);
        if (contexto.EmpresaId is not Guid empresaId || empresaId == Guid.Empty)
        {
            throw new EmpresaDivergenteException();
        }

        if (banco.Database.CurrentTransaction is not null || banco.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("A mutação deve começar na unidade de trabalho, sem alterações pendentes.");
        }

        for (var tentativa = 0; ; tentativa++)
        {
            cancelamento.ThrowIfCancellationRequested();
            var commitIniciado = false;
            try
            {
                banco.ChangeTracker.Clear();
                await using var transacao = await banco.Database.BeginTransactionAsync(cancelamento);
                var encontrada = await banco.Database.SqlQuery<Guid>(
                        $"SELECT [Id] AS [Value] FROM [Empresas] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {empresaId}")
                    .SingleOrDefaultAsync(cancelamento);
                if (encontrada != empresaId || contexto.EmpresaId != empresaId)
                {
                    throw new EmpresaDivergenteException();
                }

                var resultado = await comando(cancelamento);
                if (contexto.EmpresaId != empresaId)
                {
                    throw new EmpresaDivergenteException();
                }

                await banco.SaveChangesAsync(cancelamento);
                commitIniciado = true;
                await transacao.CommitAsync(cancelamento);
                return resultado;
            }
            catch (Exception erro) when (!commitIniciado && tentativa < 2 && Transitoria(erro))
            {
                banco.ChangeTracker.Clear();
                // Não registrar a exceção SQL: ela pode conter valores pessoais.
                logger.LogWarning("Transação cadastral será repetida. Tentativa {Tentativa}.", tentativa + 2);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (tentativa + 1)), cancelamento);
            }
            catch (DbUpdateConcurrencyException)
            {
                banco.ChangeTracker.Clear();
                throw new PrecondicaoCadastroException(ausente: false);
            }
            catch (DbUpdateException erro) when (erro.InnerException is SqlException { Number: 2601 or 2627 or 547 })
            {
                banco.ChangeTracker.Clear();
                throw Conflito(erro.InnerException.Message);
            }
            catch
            {
                banco.ChangeTracker.Clear();
                throw;
            }
        }
    }

    private static bool Transitoria(Exception erro)
    {
        var sql = erro as SqlException ?? erro.InnerException as SqlException;
        return sql is not null && (sql.Number == 1205 || sql.IsTransient);
    }

    private static ConflitoCadastroException Conflito(string mensagem)
    {
        foreach (var (indice, campo) in new[]
        {
            ("IX_Veiculos_EmpresaId_PlacaNormalizada", "placa"),
            ("IX_Clientes_EmpresaId_CpfNormalizado", "cpf"),
            ("IX_Clientes_EmpresaId_CnpjNormalizado", "cnpj"),
            ("IX_Condutores_EmpresaId_CpfNormalizado", "cpf"),
            ("IX_Planos_EmpresaId_GrupoId_Modalidade", "modalidade"),
        })
        {
            if (mensagem.Contains(indice, StringComparison.Ordinal))
            {
                return new ConflitoCadastroException("cadastro_duplicado", campo);
            }
        }

        return new ConflitoCadastroException("cadastro_conflito");
    }
}
