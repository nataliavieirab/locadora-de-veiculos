using Locadora.Application.Modulos.Transversal;

namespace Locadora.Infrastructure.Persistencia;

public static class FalhasArmazenamento
{
    public static bool Eh(Exception excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is ArmazenamentoIndisponivelException or TimeoutException or System.IO.IOException or System.Net.Sockets.SocketException)
            {
                return true;
            }

            if (atual.GetType().Name != "SqlException")
            {
                continue;
            }

            var numero = atual.GetType().GetProperty("Number")?.GetValue(atual) as int?;
            if (numero is 2601 or 2627 or 547)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
