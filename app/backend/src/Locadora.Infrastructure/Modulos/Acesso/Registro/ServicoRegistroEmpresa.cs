using Locadora.Application.Modulos.Acesso.Registro;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Acesso.Registro;

public sealed class ServicoRegistroEmpresa : IRegistroEmpresa, IConsultaUsername
{
    private readonly LocadoraDbContext _db;
    private readonly UserManager<UsuarioConta> _usuarios;
    private readonly ContextoEmpresa _contexto;
    private readonly IGanchoRegistro _gancho;
    private readonly TimeProvider _tempo;

    public ServicoRegistroEmpresa(
        LocadoraDbContext db,
        UserManager<UsuarioConta> usuarios,
        ContextoEmpresa contexto,
        IGanchoRegistro gancho,
        TimeProvider tempo)
    {
        _db = db;
        _usuarios = usuarios;
        _contexto = contexto;
        _gancho = gancho;
        _tempo = tempo;
    }

    public async Task<bool> ExisteNormalizadoAsync(string usernameNormalizado, CancellationToken cancelamento)
    {
        var existente = await _usuarios.FindByNameAsync(usernameNormalizado);
        return existente is not null;
    }

    public async Task<ResultadoRegistro> RegistrarAsync(
        string username,
        string email,
        string password,
        CancellationToken cancelamento)
    {
        var empresa = Empresa.Nova();
        _contexto.PermiteCriarEmpresa = true;
        _contexto.EmpresaId = empresa.Id;

        await using var transacao = await _db.Database.BeginTransactionAsync(cancelamento);
        try
        {
            _db.Empresas.Add(empresa);
            await _db.SaveChangesAsync(cancelamento);

            var conta = new UsuarioConta
            {
                Id = Guid.NewGuid(),
                UserName = username,
                Email = email,
                EmpresaId = empresa.Id,
                LockoutEnabled = true,
            };

            var criado = await _usuarios.CreateAsync(conta, password);
            if (!criado.Succeeded)
            {
                if (criado.Errors.Any(erro => erro.Code == "DuplicateUserName"))
                {
                    return new ResultadoRegistro.Conflito("username", "Já existe uma conta com este usuário.");
                }

                return new ResultadoRegistro.Invalido(
                    [new ErroCampo("username", "Não foi possível criar a conta.")]);
            }

            _db.AuditoriasAcesso.Add(AuditoriaAcesso.Registrar(empresa.Id, conta.Id, _tempo.GetUtcNow()));
            await _gancho.DepoisDaContaAsync(cancelamento);
            await _db.SaveChangesAsync(cancelamento);
            await transacao.CommitAsync(cancelamento);
            return new ResultadoRegistro.Criado(empresa.Id, conta.Id);
        }
        catch (DbUpdateException excecao) when (ViolacaoUnica(excecao))
        {
            return new ResultadoRegistro.Conflito("username", "Já existe uma conta com este usuário.");
        }
        catch (Exception excecao) when (FalhasArmazenamento.Eh(excecao))
        {
            throw new ArmazenamentoIndisponivelException();
        }
    }

    private static bool ViolacaoUnica(DbUpdateException excecao)
    {
        for (var atual = excecao.InnerException; atual is not null; atual = atual.InnerException)
        {
            if (atual.GetType().Name != "SqlException")
            {
                continue;
            }

            var numero = atual.GetType().GetProperty("Number")?.GetValue(atual) as int?;
            return numero is 2601 or 2627;
        }

        return false;
    }
}
