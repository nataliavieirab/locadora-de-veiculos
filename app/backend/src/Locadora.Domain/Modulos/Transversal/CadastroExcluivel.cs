namespace Locadora.Domain.Modulos.Transversal;

public abstract class CadastroExcluivel : EntidadeCadastro
{
    public DateTimeOffset? ExcluidoEmUtc { get; private set; }

    protected CadastroExcluivel()
    {
    }

    protected CadastroExcluivel(Guid empresaId, DateTimeOffset agora)
        : base(empresaId, agora)
    {
    }

    public void Excluir(DateTimeOffset agora)
    {
        ExigirAtivo();
        RegistrarAlteracao(agora);
        ExcluidoEmUtc = AlteradoEmUtc;
    }

    protected void ExigirAtivo()
    {
        if (ExcluidoEmUtc is not null)
        {
            throw new InvalidOperationException("O cadastro já foi excluído.");
        }
    }
}
