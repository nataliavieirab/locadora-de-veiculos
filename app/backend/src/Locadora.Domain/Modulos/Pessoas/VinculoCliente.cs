using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Pessoas;

public sealed class VinculoCliente : CadastroExcluivel
{
    public Guid ClientePfId { get; private set; }
    public Guid ClientePjId { get; private set; }

    private VinculoCliente()
    {
    }

    private VinculoCliente(Cliente pf, Cliente pj, DateTimeOffset agora) : base(pf.EmpresaId, agora)
    {
        if (pf.Tipo != TipoCliente.PF || pj.Tipo != TipoCliente.PJ)
        {
            throw new ValidacaoCadastroException("clienteId", "O vínculo exige um cliente PF e um cliente PJ.");
        }

        if (pf.EmpresaId != pj.EmpresaId || pf.ExcluidoEmUtc is not null || pj.ExcluidoEmUtc is not null)
        {
            throw new ValidacaoCadastroException("clienteId", "Selecione clientes ativos da mesma empresa.");
        }

        ClientePfId = pf.Id;
        ClientePjId = pj.Id;
    }

    public static VinculoCliente Novo(Cliente pf, Cliente pj, DateTimeOffset agora) => new(pf, pj, agora);
}
