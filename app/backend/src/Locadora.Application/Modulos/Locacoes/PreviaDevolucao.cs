using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;

namespace Locadora.Application.Modulos.Locacoes;

public sealed record ApuracaoDevolucao(DadosDevolucao Dados, ResultadoCalculoFinal Calculo,
    VersoesDevolucaoAceitas Versoes);

public sealed class PreviaDevolucao(IConsultasDevolucao consultas, IContextoLocacao contexto)
{
    public async Task<ResultadoPreviaDevolucao> PreviaAsync(Guid id, ComandoDevolucao comando,
        string? versao, CancellationToken cancelamento)
    {
        comando.Validar();
        comando = comando with { TaxasAdicionaisIds = comando.TaxasAdicionaisIds.ToArray() };
        var aluguel = await consultas.AluguelAsync(id, cancelamento) ?? throw new CadastroNaoEncontradoException();
        VersaoCadastro.Exigir(versao, aluguel.RowVersion);
        var apuracao = await ApurarAsync(aluguel, comando, cancelamento);
        var atual = await consultas.VersaoAluguelAsync(id, cancelamento) ?? throw new CadastroNaoEncontradoException();
        if (!atual.SequenceEqual(aluguel.RowVersion)) throw new PrecondicaoCadastroException(ausente: false);
        return new(id, aluguel.RevisaoAtual.Id, aluguel.NumeroRevisaoAtual, aluguel.RowVersion.ToArray(),
            apuracao.Calculo, apuracao.Versoes);
    }

    public async Task<ApuracaoDevolucao> ApurarAsync(Aluguel aluguel, ComandoDevolucao comando,
        CancellationToken cancelamento, bool confirmarAceite = false)
    {
        comando.Validar();
        comando = comando with { TaxasAdicionaisIds = comando.TaxasAdicionaisIds.ToArray() };
        if (aluguel.EmpresaId != contexto.EmpresaId) throw new CadastroNaoEncontradoException();
        if (aluguel.Estado != EstadoAluguel.EmAberto) throw new ConflitoCadastroException("aluguel_nao_devolvivel");
        var dados = await consultas.CarregarAsync(aluguel, comando, cancelamento, confirmarAceite);
        var calculo = aluguel.CalcularDevolucao(dados);
        var versoes = AceiteDevolucao.Criar(contexto.EmpresaId, contexto.ContaId, aluguel, dados, calculo);
        return new(dados, calculo, versoes);
    }

    // Deve ser chamado dentro da unidade/lock na confirmação; nunca usa um total enviado pelo cliente.
    public async Task<ApuracaoDevolucao> RecalcularAceiteAsync(Aluguel aluguel, ComandoConclusaoDevolucao comando,
        CancellationToken cancelamento)
    {
        AceiteDevolucao.Validar(comando.VersoesAceitas);
        var apuracao = await ApurarAsync(aluguel, comando.Dados, cancelamento, confirmarAceite: true);
        AceiteDevolucao.Exigir(comando.VersoesAceitas, apuracao.Versoes);
        return apuracao;
    }
}
