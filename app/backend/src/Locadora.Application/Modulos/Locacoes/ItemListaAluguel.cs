using Locadora.Domain.Modulos.Locacoes;

namespace Locadora.Application.Modulos.Locacoes;

public sealed record ItemListaAluguel(Guid Id, EstadoAluguel Estado, int Revisao, string CondutorNome,
    string CondutorCpfResumo, string VeiculoPlaca, string PlanoModalidade, DateTimeOffset SaidaUtc,
    DateTimeOffset RetornoPrevistoUtc, decimal TotalConhecido)
{
    public static ItemListaAluguel De(Aluguel aluguel, RevisaoContrato revisao) => new(
        aluguel.Id, aluguel.Estado, revisao.Numero, revisao.Condutor.Nome,
        ResumoDocumento.Cpf(revisao.Condutor.Cpf), revisao.Veiculo.Placa,
        revisao.Plano.Modalidade.ToString(), aluguel.SaidaUtc, aluguel.RetornoPrevistoUtc,
        revisao.Estimativa.TotalConhecido);
}