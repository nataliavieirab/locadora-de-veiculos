namespace Locadora.Domain.Acesso;

public static class PrazosSessao
{
    public static readonly TimeSpan DuracaoAbsoluta = TimeSpan.FromHours(8);
    public static readonly TimeSpan AcessoMaximo = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ToleranciaRelogio = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan BloqueioConta = TimeSpan.FromMinutes(15);

    public static DateTimeOffset ExpiracaoAcesso(DateTimeOffset agora, DateTimeOffset fimSessao)
    {
        var peloAcesso = agora.Add(AcessoMaximo);
        return peloAcesso <= fimSessao ? peloAcesso : fimSessao;
    }
}
