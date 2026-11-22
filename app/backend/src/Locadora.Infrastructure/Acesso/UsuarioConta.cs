using Locadora.Domain.Transversal;
using Microsoft.AspNetCore.Identity;

namespace Locadora.Infrastructure.Acesso;

public sealed class UsuarioConta : IdentityUser<Guid>, IEntidadeEmpresa
{
    public Guid EmpresaId { get; set; }
}
