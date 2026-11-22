using Locadora.Domain.Modulos.Transversal;
using Microsoft.AspNetCore.Identity;

namespace Locadora.Infrastructure.Modulos.Acesso.Registro;

public sealed class UsuarioConta : IdentityUser<Guid>, IEntidadeEmpresa
{
    public Guid EmpresaId { get; set; }
}
