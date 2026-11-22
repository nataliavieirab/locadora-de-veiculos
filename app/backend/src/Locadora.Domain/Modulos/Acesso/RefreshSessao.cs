using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Acesso;

public sealed class RefreshSessao : IEntidadeEmpresa
{
    public Guid Id { get; private set; }
    public Guid SessaoId { get; private set; }
    public Guid FamiliaId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Hash { get; private set; } = "";
    public DateTimeOffset EmitidoEm { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public DateTimeOffset? ConsumidoEm { get; private set; }

    private RefreshSessao()
    {
    }

    public static RefreshSessao Emitir(
        Guid sessaoId,
        Guid familiaId,
        Guid empresaId,
        string hash,
        DateTimeOffset agora,
        DateTimeOffset expiraEm)
    {
        return new RefreshSessao
        {
            Id = Guid.NewGuid(),
            SessaoId = sessaoId,
            FamiliaId = familiaId,
            EmpresaId = empresaId,
            Hash = hash,
            EmitidoEm = agora,
            ExpiraEm = expiraEm,
        };
    }

    public void Consumir(DateTimeOffset agora)
    {
        ConsumidoEm ??= agora;
    }
}
