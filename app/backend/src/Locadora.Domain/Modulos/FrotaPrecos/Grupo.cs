using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.FrotaPrecos;

public sealed class Grupo : CadastroExcluivel
{
    public string Nome { get; private set; } = "";

    private Grupo()
    {
    }

    private Grupo(Guid empresaId, string nome, DateTimeOffset agora) : base(empresaId, agora)
    {
        Nome = ValidacaoCadastro.Texto(nome, "nome", 200);
    }

    public static Grupo Novo(Guid empresaId, string nome, DateTimeOffset agora) => new(empresaId, nome, agora);

    public void Alterar(string nome, DateTimeOffset agora)
    {
        ExigirAtivo();
        var validado = ValidacaoCadastro.Texto(nome, "nome", 200);
        RegistrarAlteracao(agora);
        Nome = validado;
    }
}
