using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public sealed class DemonstrativoEstimado : IEntidadeEmpresa
{
    private readonly List<ComponenteEstimativa> _componentes = [];
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid RevisaoId { get; private set; }
    public string VersaoRegra { get; private set; } = "";
    public DateTimeOffset CalculadoEmUtc { get; private set; }
    public DateTimeOffset SaidaUtc { get; private set; }
    public DateTimeOffset RetornoPrevistoUtc { get; private set; }
    public int Diarias { get; private set; }
    public int? Quilometros { get; private set; }
    public long? FranquiaTotalKm { get; private set; }
    public bool QuilometragemPendente { get; private set; }
    public bool CombustivelPendente { get; private set; }
    public bool AtrasoPendente { get; private set; }
    public decimal TermoDiariasBruto { get; private set; }
    public decimal TermoKmBruto { get; private set; }
    public decimal TotalConhecido { get; private set; }
    public IReadOnlyCollection<ComponenteEstimativa> Componentes => _componentes.AsReadOnly();

    private DemonstrativoEstimado() { }

    internal static DemonstrativoEstimado Calcular(Guid empresaId, Guid revisaoId, DateTimeOffset saida,
        DateTimeOffset retorno, DateTimeOffset agora, Guid planoId, ParametrosPlano plano,
        IReadOnlyCollection<AdicionalCalculo> adicionais)
    {
        var calculo = CalculadoraLocacao.Calcular(saida, retorno, planoId, plano, adicionais);
        var demonstrativo = new DemonstrativoEstimado
        {
            Id = Guid.NewGuid(), EmpresaId = empresaId, RevisaoId = revisaoId,
            VersaoRegra = ResultadoCalculo.VersaoRegra, CalculadoEmUtc = agora.ToUniversalTime(),
            SaidaUtc = saida.ToUniversalTime(), RetornoPrevistoUtc = retorno.ToUniversalTime(),
            Diarias = calculo.Diarias, Quilometros = calculo.Quilometros, FranquiaTotalKm = calculo.FranquiaTotalKm,
            QuilometragemPendente = calculo.QuilometragemPendente,
            CombustivelPendente = calculo.CombustivelPendente, AtrasoPendente = calculo.AtrasoPendente,
            TermoDiariasBruto = calculo.TermoDiariasBruto, TermoKmBruto = calculo.TermoKmBruto,
            TotalConhecido = calculo.TotalConhecido,
        };
        demonstrativo._componentes.AddRange(calculo.Componentes.Select((componente, indice) =>
            ComponenteEstimativa.Capturar(empresaId, demonstrativo.Id, indice, componente)));
        return demonstrativo;
    }
}

public sealed class ComponenteEstimativa : IEntidadeEmpresa
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid DemonstrativoId { get; private set; }
    public int Ordem { get; private set; }
    public CategoriaItem? Categoria { get; private set; }
    public Guid OrigemId { get; private set; }
    public TipoSeguro? TipoSeguro { get; private set; }
    public string Nome { get; private set; } = "";
    public decimal PrecoUnitario { get; private set; }
    public int Multiplicador { get; private set; }
    public decimal ValorBruto { get; private set; }
    public decimal ValorArredondado { get; private set; }

    private ComponenteEstimativa() { }

    internal static ComponenteEstimativa Capturar(Guid empresaId, Guid demonstrativoId, int ordem,
        ComponenteCalculado componente) => new()
    {
        Id = Guid.NewGuid(), EmpresaId = empresaId, DemonstrativoId = demonstrativoId, Ordem = ordem,
        Categoria = componente.Categoria, OrigemId = componente.OrigemId, TipoSeguro = componente.TipoSeguro,
        Nome = componente.Nome, PrecoUnitario = componente.PrecoUnitario, Multiplicador = componente.Multiplicador,
        ValorBruto = componente.ValorBruto, ValorArredondado = componente.ValorArredondado,
    };
}
