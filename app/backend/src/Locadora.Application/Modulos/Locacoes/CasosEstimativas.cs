using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;

namespace Locadora.Application.Modulos.Locacoes;

public sealed record PreviaLocacao(DateTimeOffset SaidaUtc, RevisaoContrato Revisao);

public sealed class CasosEstimativas(IConsultasContratacao consultas, IContextoCadastro contexto, TimeProvider tempo)
{
    public async Task<PreviaLocacao> EstimarAsync(ComandoContratacao comando, Guid? aluguelId,
        string? versao, CancellationToken cancelamento)
    {
        comando.Validar();
        Aluguel? aluguel = null;
        if (aluguelId is Guid id)
        {
            aluguel = await consultas.AluguelAsync(id, cancelamento) ?? throw new CadastroNaoEncontradoException();
            VersaoCadastro.Exigir(versao, aluguel.RowVersion);
        }
        var dados = await consultas.CarregarAsync(comando, cancelamento);
        if (await consultas.VeiculoOcupadoAsync(comando.VeiculoId, aluguelId, cancelamento))
            throw new ConflitoCadastroException("veiculo_indisponivel", "veiculoId");
        var agora = tempo.GetUtcNow();
        if (aluguel is not null)
            return new(aluguel.SaidaUtc, aluguel.PrepararEdicao(dados, agora));
        return new(agora, RevisaoContrato.Preparar(contexto.EmpresaId, Guid.NewGuid(), agora, dados, agora));
    }
}

public sealed class CasosSelecoesLocacao(IConsultasContratacao consultas)
{
    public Task<Pagina<Domain.Modulos.FrotaPrecos.Veiculo>> VeiculosAsync(Paginacao paginacao, Guid? grupoId,
        bool somenteDisponiveis, Guid? aluguelEmEdicaoId, CancellationToken cancelamento)
        => consultas.VeiculosAsync(paginacao, grupoId, somenteDisponiveis, aluguelEmEdicaoId, cancelamento);

    public Task<Pagina<Domain.Modulos.Pessoas.Condutor>> CondutoresAsync(Paginacao paginacao, Guid clienteId,
        CancellationToken cancelamento) => consultas.CondutoresAsync(paginacao, clienteId, cancelamento);
}
