using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public sealed class DemonstrativoFinal : IEntidadeEmpresa
{
    private readonly List<ItemDevolucao> _itens = [];
    private readonly List<ComponenteFinal> _componentes = [];
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid AluguelId { get; private set; }
    public Guid RevisaoId { get; private set; }
    public Guid PlanoId { get; private set; }
    public PlanoContratado Plano { get; private set; } = null!;
    public DateTimeOffset SaidaUtc { get; private set; }
    public DateTimeOffset RetornoPrevistoUtc { get; private set; }
    public DateTimeOffset RetornoRealUtc { get; private set; }
    public int HodometroInicial { get; private set; }
    public int HodometroFinal { get; private set; }
    public decimal LitrosRestantes { get; private set; }
    public decimal CapacidadeLitros { get; private set; }
    public TipoCombustivel TipoCombustivel { get; private set; }
    public Guid PrecoCombustivelId { get; private set; }
    public DateTimeOffset InicioVigenciaCombustivelUtc { get; private set; }
    public decimal PrecoCombustivel { get; private set; }
    public int Diarias { get; private set; }
    public int Quilometros { get; private set; }
    public long? FranquiaTotalKm { get; private set; }
    public long? ExcedenteKm { get; private set; }
    public decimal TermoDiariasBruto { get; private set; }
    public decimal TermoKmBruto { get; private set; }
    public decimal TotalFinal { get; private set; }
    public decimal Caucao { get; private set; }
    public string VersaoRegra { get; private set; } = "";
    public DateTimeOffset CalculadoEmUtc { get; private set; }
    public DateTimeOffset RegistradoEmUtc { get; private set; }
    public IReadOnlyCollection<ItemDevolucao> Itens => _itens.AsReadOnly();
    public IReadOnlyCollection<ComponenteFinal> Componentes => _componentes.AsReadOnly();

    private DemonstrativoFinal() { }

    internal static DemonstrativoFinal Capturar(RevisaoContrato revisao, ResultadoCalculoFinal calculo,
        IReadOnlyCollection<TaxaServico> taxas, DateTimeOffset agora)
    {
        var entrada = calculo.Entradas;
        var final = new DemonstrativoFinal
        {
            Id = Guid.NewGuid(), EmpresaId = revisao.EmpresaId, AluguelId = revisao.AluguelId, RevisaoId = revisao.Id,
            PlanoId = revisao.PlanoId, Plano = revisao.Plano,
            SaidaUtc = entrada.Saida, RetornoPrevistoUtc = entrada.RetornoPrevisto, RetornoRealUtc = entrada.RetornoReal,
            HodometroInicial = entrada.HodometroInicial, HodometroFinal = entrada.HodometroFinal,
            LitrosRestantes = entrada.LitrosRestantes, CapacidadeLitros = entrada.CapacidadeLitros,
            TipoCombustivel = entrada.TipoCombustivel, PrecoCombustivelId = entrada.Combustivel!.Id,
            InicioVigenciaCombustivelUtc = entrada.Combustivel.InicioVigenciaUtc, PrecoCombustivel = entrada.Combustivel.PrecoUnitario,
            Diarias = calculo.Diarias, Quilometros = calculo.Quilometros, FranquiaTotalKm = calculo.FranquiaTotalKm,
            ExcedenteKm = calculo.ExcedenteKm, TermoDiariasBruto = calculo.TermoDiariasBruto, TermoKmBruto = calculo.TermoKmBruto,
            TotalFinal = calculo.TotalFinal, Caucao = ResultadoCalculoFinal.Caucao, VersaoRegra = ResultadoCalculoFinal.VersaoRegra,
            CalculadoEmUtc = agora.ToUniversalTime(), RegistradoEmUtc = agora.ToUniversalTime(),
        };
        foreach (var adicional in entrada.Adicionais.Where(a => a.OrigemInclusao == OrigemInclusao.Devolucao))
            final._itens.Add(ItemDevolucao.Capturar(final.EmpresaId, final.Id, adicional.Item,
                taxas.Single(t => t.Id == adicional.Item.OrigemId).RowVersion));
        foreach (var (componente, ordem) in calculo.Componentes.Select((c, i) => (c, i)))
            final._componentes.Add(ComponenteFinal.Capturar(final.EmpresaId, final.Id, ordem, componente));
        return final;
    }
}

public sealed class ItemDevolucao : IEntidadeEmpresa
{
    private byte[] _versaoTaxa = [];
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid DemonstrativoFinalId { get; private set; }
    public Guid TaxaServicoId { get; private set; }
    public string Nome { get; private set; } = "";
    public ModalidadeTaxa Modalidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public OrigemInclusao OrigemInclusao { get; private set; }
    public byte[] VersaoTaxa { get => _versaoTaxa.ToArray(); private set => _versaoTaxa = value.ToArray(); }

    private ItemDevolucao() { }

    internal static ItemDevolucao Capturar(Guid empresaId, Guid finalId, AdicionalCalculo item, byte[] versao) => new()
    {
        Id = Guid.NewGuid(), EmpresaId = empresaId, DemonstrativoFinalId = finalId, TaxaServicoId = item.OrigemId,
        Nome = item.Nome, Modalidade = item.Modalidade, PrecoUnitario = item.PrecoUnitario,
        OrigemInclusao = OrigemInclusao.Devolucao, VersaoTaxa = versao,
    };
}

public sealed class ComponenteFinal : IEntidadeEmpresa
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid DemonstrativoFinalId { get; private set; }
    public int Ordem { get; private set; }
    public CategoriaComponenteFinal Categoria { get; private set; }
    public Guid? OrigemId { get; private set; }
    public TipoSeguro? TipoSeguro { get; private set; }
    public string Nome { get; private set; } = "";
    public ModalidadeTaxa? Modalidade { get; private set; }
    public OrigemInclusao? OrigemInclusao { get; private set; }
    public decimal Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal ValorBruto { get; private set; }
    public decimal ValorArredondado { get; private set; }

    private ComponenteFinal() { }

    internal static ComponenteFinal Capturar(Guid empresaId, Guid finalId, int ordem, ComponenteFinalCalculado calculo) => new()
    {
        Id = Guid.NewGuid(), EmpresaId = empresaId, DemonstrativoFinalId = finalId, Ordem = ordem,
        Categoria = calculo.Categoria, OrigemId = calculo.OrigemId, TipoSeguro = calculo.TipoSeguro, Nome = calculo.Nome,
        Modalidade = calculo.Modalidade, OrigemInclusao = calculo.OrigemInclusao, Quantidade = calculo.Quantidade,
        PrecoUnitario = calculo.PrecoUnitario, ValorBruto = calculo.ValorBruto, ValorArredondado = calculo.ValorArredondado,
    };
}
