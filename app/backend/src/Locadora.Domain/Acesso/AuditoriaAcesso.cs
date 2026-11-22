using Locadora.Domain.Transversal;

namespace Locadora.Domain.Acesso;

public sealed class AuditoriaAcesso : IEntidadeEmpresa
{
    public const string AcaoRegistro = "registro";

    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid ContaId { get; private set; }
    public DateTimeOffset Momento { get; private set; }
    public string Acao { get; private set; } = "";

    private AuditoriaAcesso()
    {
    }

    public static AuditoriaAcesso Registrar(Guid empresaId, Guid contaId, DateTimeOffset momento)
    {
        return new AuditoriaAcesso
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            ContaId = contaId,
            Momento = momento,
            Acao = AcaoRegistro,
        };
    }
}
