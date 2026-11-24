using Locadora.Domain.Modulos.Locacoes;

namespace Locadora.Application.Modulos.Locacoes;

public sealed record FiltroConsultaAlugueis(EstadoAluguel? Estado = null, Guid? VeiculoId = null,
    DateTimeOffset? SaidaDe = null, DateTimeOffset? SaidaAte = null);