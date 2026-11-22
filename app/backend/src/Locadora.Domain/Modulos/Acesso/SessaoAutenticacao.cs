using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Acesso;

public sealed class SessaoAutenticacao : IEntidadeEmpresa
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid FamiliaId { get; private set; }
    public int VersaoAcesso { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public DateTimeOffset? RevogadaEm { get; private set; }

    private SessaoAutenticacao()
    {
    }

    public static SessaoAutenticacao Abrir(Guid contaId, Guid empresaId, DateTimeOffset agora)
    {
        return new SessaoAutenticacao
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            EmpresaId = empresaId,
            FamiliaId = Guid.NewGuid(),
            VersaoAcesso = 1,
            CriadaEm = agora,
            ExpiraEm = agora.Add(PrazosSessao.DuracaoAbsoluta),
        };
    }

    public bool Vigente(DateTimeOffset agora) => RevogadaEm is null && agora < ExpiraEm;

    public void Revogar(DateTimeOffset agora)
    {
        RevogadaEm ??= agora;
    }

    public int AvancarVersao()
    {
        VersaoAcesso++;
        return VersaoAcesso;
    }
}
