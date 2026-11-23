namespace Locadora.Domain.Modulos.Transversal;

public abstract class EntidadeCadastro : IEntidadeEmpresa
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public DateTimeOffset CriadoEmUtc { get; private set; }
    public DateTimeOffset AlteradoEmUtc { get; private set; }

    // O banco gera a versão; o domínio não simula o contador do SQL Server.
    public byte[] RowVersion { get; private set; } = [];

    protected EntidadeCadastro()
    {
    }

    protected EntidadeCadastro(Guid empresaId, DateTimeOffset agora)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("Informe a empresa do cadastro.", nameof(empresaId));
        }

        Id = Guid.NewGuid();
        EmpresaId = empresaId;
        CriadoEmUtc = agora.ToUniversalTime();
        AlteradoEmUtc = CriadoEmUtc;
    }

    protected void RegistrarAlteracao(DateTimeOffset agora)
    {
        var instante = agora.ToUniversalTime();
        if (instante < AlteradoEmUtc)
        {
            throw new ArgumentException("A alteração não pode preceder a versão atual do cadastro.", nameof(agora));
        }

        AlteradoEmUtc = instante;
    }
}
