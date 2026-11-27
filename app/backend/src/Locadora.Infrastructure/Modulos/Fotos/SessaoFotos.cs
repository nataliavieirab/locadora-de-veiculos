using Locadora.Application.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Fotos;

// Identidade extraída do token validado pelo servidor no composition root,
// nunca de DTO/multipart. Nova instância por requisição.
public sealed class SessaoFotos(LocadoraDbContext banco, ContextoEmpresa contexto,
    Guid sessaoId, int versaoAcesso, TimeProvider tempo) : ISessaoFotos
{
    public Guid AtorId => contexto.ContaId ?? throw new SessaoFotoInvalidaException();
    public string Correlacao => contexto.CorrelacaoId?.ToString("N") ?? throw new SessaoFotoInvalidaException();

    public async Task ExigirAsync(CancellationToken cancelamento)
    {
        var sessao = await banco.SessoesAutenticacao.AsNoTracking().SingleOrDefaultAsync(s =>
            s.Id == sessaoId && s.ContaId == AtorId && s.VersaoAcesso == versaoAcesso, cancelamento);
        if (sessao is null || !sessao.Vigente(tempo.GetUtcNow())
            || !await banco.Users.AsNoTracking().AnyAsync(u => u.Id == AtorId && u.EmpresaId == contexto.EmpresaId, cancelamento))
            throw new SessaoFotoInvalidaException();
    }
}
