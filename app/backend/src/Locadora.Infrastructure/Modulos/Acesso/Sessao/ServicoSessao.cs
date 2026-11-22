using System.Data;
using Locadora.Application.Modulos.Acesso.Sessao;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Infrastructure.Persistencia;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.Transversal;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Acesso.Sessao;

public sealed class ServicoSessao : IServicoSessao
{
    private readonly LocadoraDbContext _db;
    private readonly UserManager<UsuarioConta> _usuarios;
    private readonly IPasswordHasher<UsuarioConta> _hasher;
    private readonly ContextoEmpresa _contexto;
    private readonly EmissorJwt _emissor;
    private readonly TimeProvider _tempo;
    private readonly UsuarioConta _contaFicticia = new();
    private readonly string _hashFicticio;

    public ServicoSessao(
        LocadoraDbContext db,
        UserManager<UsuarioConta> usuarios,
        IPasswordHasher<UsuarioConta> hasher,
        ContextoEmpresa contexto,
        EmissorJwt emissor,
        TimeProvider tempo)
    {
        _db = db;
        _usuarios = usuarios;
        _hasher = hasher;
        _contexto = contexto;
        _emissor = emissor;
        _tempo = tempo;
        _hashFicticio = hasher.HashPassword(_contaFicticia, "temporizacao-local");
    }

    public async Task<ResultadoLogin> EntrarAsync(string? username, string? password, CancellationToken cancelamento)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                _hasher.VerifyHashedPassword(_contaFicticia, _hashFicticio, password ?? string.Empty);
                return ResultadoLogin.CredencialInvalida.Instancia;
            }

            var usuario = await _usuarios.FindByNameAsync(username.Trim());
            if (usuario is null)
            {
                _hasher.VerifyHashedPassword(_contaFicticia, _hashFicticio, password);
                return ResultadoLogin.CredencialInvalida.Instancia;
            }

            _contexto.EmpresaId = usuario.EmpresaId;
            _contexto.PermiteCriarEmpresa = false;

            if (await _usuarios.IsLockedOutAsync(usuario))
            {
                return ResultadoLogin.CredencialInvalida.Instancia;
            }

            if (!await _usuarios.CheckPasswordAsync(usuario, password))
            {
                await _usuarios.AccessFailedAsync(usuario);
                return ResultadoLogin.CredencialInvalida.Instancia;
            }

            await _usuarios.ResetAccessFailedCountAsync(usuario);
            var agora = _tempo.GetUtcNow();
            var sessao = SessaoAutenticacao.Abrir(usuario.Id, usuario.EmpresaId, agora);
            var opaco = GeradorRefresh.Novo();
            var refresh = RefreshSessao.Emitir(
                sessao.Id,
                sessao.FamiliaId,
                sessao.EmpresaId,
                GeradorRefresh.Hash(opaco),
                agora,
                sessao.ExpiraEm);

            _db.SessoesAutenticacao.Add(sessao);
            _db.RefreshesSessao.Add(refresh);
            await _db.SaveChangesAsync(cancelamento);

            var expiraAcesso = PrazosSessao.ExpiracaoAcesso(agora, sessao.ExpiraEm);
            var token = _emissor.Emitir(usuario.Id, usuario.EmpresaId, sessao.Id, sessao.VersaoAcesso, expiraAcesso);
            return new ResultadoLogin.Ok(token, opaco, expiraAcesso, usuario.Id, usuario.EmpresaId);
        }
        catch (Exception excecao) when (FalhasArmazenamento.Eh(excecao))
        {
            return ResultadoLogin.Indisponivel.Instancia;
        }
    }

    public async Task<ResultadoRenovacao> RenovarAsync(string? refresh, CancellationToken cancelamento)
    {
        if (string.IsNullOrWhiteSpace(refresh))
        {
            return ResultadoRenovacao.Recusado.Instancia;
        }

        try
        {
            var hash = GeradorRefresh.Hash(refresh);
            await using var transacao = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancelamento);
            var existente = await _db.RefreshesSessao.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Hash == hash, cancelamento);
            if (existente is null)
            {
                return ResultadoRenovacao.Recusado.Instancia;
            }

            var sessao = await _db.SessoesAutenticacao.IgnoreQueryFilters()
                .FirstAsync(item => item.Id == existente.SessaoId, cancelamento);
            _contexto.EmpresaId = sessao.EmpresaId;
            _contexto.PermiteCriarEmpresa = false;
            var agora = _tempo.GetUtcNow();

            if (existente.ConsumidoEm is not null)
            {
                await RevogarFamiliaAsync(sessao.FamiliaId, agora, cancelamento);
                await _db.SaveChangesAsync(cancelamento);
                await transacao.CommitAsync(cancelamento);
                return ResultadoRenovacao.Recusado.Instancia;
            }

            var usuario = await _usuarios.FindByIdAsync(sessao.ContaId.ToString());
            if (usuario is null || usuario.EmpresaId != sessao.EmpresaId || !sessao.Vigente(agora))
            {
                await RevogarFamiliaAsync(sessao.FamiliaId, agora, cancelamento);
                await _db.SaveChangesAsync(cancelamento);
                await transacao.CommitAsync(cancelamento);
                return ResultadoRenovacao.Recusado.Instancia;
            }

            existente.Consumir(agora);
            var versao = sessao.AvancarVersao();
            var opaco = GeradorRefresh.Novo();
            _db.RefreshesSessao.Add(RefreshSessao.Emitir(
                sessao.Id,
                sessao.FamiliaId,
                sessao.EmpresaId,
                GeradorRefresh.Hash(opaco),
                agora,
                sessao.ExpiraEm));
            await _db.SaveChangesAsync(cancelamento);
            await transacao.CommitAsync(cancelamento);

            var expiraAcesso = PrazosSessao.ExpiracaoAcesso(agora, sessao.ExpiraEm);
            var token = _emissor.Emitir(usuario.Id, usuario.EmpresaId, sessao.Id, versao, expiraAcesso);
            return new ResultadoRenovacao.Ok(token, opaco, expiraAcesso, usuario.Id, usuario.EmpresaId);
        }
        catch (Exception excecao) when (FalhasArmazenamento.Eh(excecao))
        {
            return ResultadoRenovacao.Indisponivel.Instancia;
        }
    }

    public async Task<ResultadoLogout> SairAsync(Guid sessaoId, CancellationToken cancelamento)
    {
        try
        {
            var sessao = await _db.SessoesAutenticacao.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == sessaoId, cancelamento);
            if (sessao is null)
            {
                return ResultadoLogout.Recusado.Instancia;
            }

            _contexto.EmpresaId = sessao.EmpresaId;
            _contexto.PermiteCriarEmpresa = false;
            if (sessao.RevogadaEm is not null)
            {
                return ResultadoLogout.Ok.Instancia;
            }

            await using var transacao = await _db.Database.BeginTransactionAsync(cancelamento);
            await RevogarFamiliaAsync(sessao.FamiliaId, _tempo.GetUtcNow(), cancelamento);
            await _db.SaveChangesAsync(cancelamento);
            await transacao.CommitAsync(cancelamento);
            return ResultadoLogout.Ok.Instancia;
        }
        catch (Exception excecao) when (FalhasArmazenamento.Eh(excecao))
        {
            return ResultadoLogout.Indisponivel.Instancia;
        }
    }

    public async Task<ResultadoConsulta> ConsultarAsync(Guid sessaoId, CancellationToken cancelamento)
    {
        try
        {
            var sessao = await _db.SessoesAutenticacao.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == sessaoId, cancelamento);
            var agora = _tempo.GetUtcNow();
            if (sessao is null || !sessao.Vigente(agora))
            {
                return ResultadoConsulta.Recusado.Instancia;
            }

            var usuario = await _usuarios.FindByIdAsync(sessao.ContaId.ToString());
            if (usuario is null || usuario.EmpresaId != sessao.EmpresaId)
            {
                return ResultadoConsulta.Recusado.Instancia;
            }

            return new ResultadoConsulta.Ok(sessao.ContaId, sessao.EmpresaId);
        }
        catch (Exception excecao) when (FalhasArmazenamento.Eh(excecao))
        {
            return ResultadoConsulta.Indisponivel.Instancia;
        }
    }

    private async Task RevogarFamiliaAsync(Guid familiaId, DateTimeOffset agora, CancellationToken cancelamento)
    {
        var sessoes = await _db.SessoesAutenticacao.IgnoreQueryFilters()
            .Where(item => item.FamiliaId == familiaId)
            .ToListAsync(cancelamento);
        foreach (var sessao in sessoes)
        {
            sessao.Revogar(agora);
        }

        var refreshes = await _db.RefreshesSessao.IgnoreQueryFilters()
            .Where(item => item.FamiliaId == familiaId)
            .ToListAsync(cancelamento);
        foreach (var refresh in refreshes)
        {
            refresh.Consumir(agora);
        }
    }
}
