namespace Locadora.Application.Modulos.Acesso;

public sealed class LimitesRequisicao
{
    public int RegistroQuantidade { get; }
    public TimeSpan RegistroJanela { get; }
    public int LoginQuantidade { get; }
    public TimeSpan LoginJanela { get; }

    public LimitesRequisicao(int registroQuantidade, TimeSpan registroJanela, int loginQuantidade, TimeSpan loginJanela)
    {
        RegistroQuantidade = registroQuantidade;
        RegistroJanela = registroJanela;
        LoginQuantidade = loginQuantidade;
        LoginJanela = loginJanela;
    }

    public static LimitesRequisicao Padrao { get; } = new(5, TimeSpan.FromMinutes(10), 5, TimeSpan.FromMinutes(1));
}
