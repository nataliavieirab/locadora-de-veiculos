namespace Locadora.Application.Modulos.Acesso.Sessao;

public interface IServicoSessao
{
    Task<ResultadoLogin> EntrarAsync(string? username, string? password, CancellationToken cancelamento);

    Task<ResultadoRenovacao> RenovarAsync(string? refresh, CancellationToken cancelamento);

    Task<ResultadoLogout> SairAsync(Guid sessaoId, CancellationToken cancelamento);

    Task<ResultadoConsulta> ConsultarAsync(Guid sessaoId, CancellationToken cancelamento);
}
