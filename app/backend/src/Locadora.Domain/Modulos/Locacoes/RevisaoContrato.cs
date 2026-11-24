using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public sealed record DadosContratacao(Cliente Cliente, Condutor Condutor, Veiculo Veiculo, Grupo Grupo,
    Plano Plano, DateTimeOffset RetornoPrevisto, int HodometroInicial,
    IReadOnlyCollection<TaxaServico> Taxas, IReadOnlyCollection<PrecoSeguro> Seguros);

public sealed class RevisaoContrato : IEntidadeEmpresa
{
    private readonly List<ItemContratado> _itens = [];
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid AluguelId { get; private set; }
    public int Numero { get; private set; }
    public Guid ClienteId { get; private set; }
    public Guid CondutorId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public Guid PlanoId { get; private set; }
    public Guid GrupoId { get; private set; }
    public DateTimeOffset CriadaEmUtc { get; private set; }
    public DateTimeOffset SaidaUtc { get; private set; }
    public DateTimeOffset RetornoPrevistoUtc { get; private set; }
    public int HodometroInicial { get; private set; }
    public decimal Caucao { get; private set; }
    public ClienteContratado Cliente { get; private set; } = null!;
    public CondutorContratado Condutor { get; private set; } = null!;
    public VeiculoContratado Veiculo { get; private set; } = null!;
    public PlanoContratado Plano { get; private set; } = null!;
    public IReadOnlyCollection<ItemContratado> Itens => _itens.AsReadOnly();
    public DemonstrativoEstimado Estimativa { get; private set; } = null!;

    private RevisaoContrato() { }

    public static RevisaoContrato Preparar(Guid empresaId, Guid aluguelId, DateTimeOffset saida,
        DadosContratacao dados, DateTimeOffset agora, RevisaoContrato? anterior = null)
    {
        ValidacaoCadastro.Referencia(empresaId, "empresaId");
        ValidacaoCadastro.Referencia(aluguelId, "aluguelId");
        if (dados.Cliente.EmpresaId != empresaId
            || anterior is not null && (anterior.EmpresaId != empresaId || anterior.AluguelId != aluguelId))
            throw new EmpresaDivergenteException();
        ElegibilidadeLocacao.ValidarContratacao(dados.Cliente, dados.Condutor, dados.Veiculo,
            dados.Grupo, dados.Plano, saida, dados.RetornoPrevisto, dados.HodometroInicial);
        if (agora < saida || anterior is not null && agora < anterior.CriadaEmUtc)
            throw new ValidacaoCadastroException("revisao", "O instante da revisão não pode preceder o contrato.");

        var atuais = new List<AdicionalCalculo>();
        foreach (var taxa in dados.Taxas)
        {
            if (taxa.EmpresaId != empresaId || taxa.ExcluidoEmUtc is not null)
                throw new ValidacaoCadastroException("taxasServicoIds", "Selecione taxas ativas da empresa.");
            atuais.Add(new(CategoriaItem.TaxaServico, taxa.Id, null, taxa.Nome, taxa.Modalidade, taxa.Preco));
        }
        foreach (var seguro in dados.Seguros)
        {
            if (seguro.EmpresaId != empresaId)
                throw new ValidacaoCadastroException("seguros", "Selecione seguros configurados da empresa.");
            atuais.Add(new(CategoriaItem.Seguro, seguro.Id, seguro.Tipo, $"Seguro {seguro.Tipo}", ModalidadeTaxa.Diaria, seguro.PrecoDiario));
        }
        var revisao = new RevisaoContrato
        {
            Id = Guid.NewGuid(), EmpresaId = empresaId, AluguelId = aluguelId,
            Numero = checked((anterior?.Numero ?? 0) + 1), CriadaEmUtc = agora.ToUniversalTime(),
            SaidaUtc = saida.ToUniversalTime(),
            ClienteId = dados.Cliente.Id, CondutorId = dados.Condutor.Id,
            VeiculoId = dados.Veiculo.Id, PlanoId = dados.Plano.Id,
            GrupoId = dados.Grupo.Id,
            RetornoPrevistoUtc = dados.RetornoPrevisto.ToUniversalTime(), HodometroInicial = dados.HodometroInicial,
            Caucao = ResultadoCalculo.Caucao,
            Cliente = anterior?.ClienteId == dados.Cliente.Id ? anterior.Cliente : ClienteContratado.Capturar(dados.Cliente),
            Condutor = anterior?.CondutorId == dados.Condutor.Id ? anterior.Condutor : CondutorContratado.Capturar(dados.Condutor),
            Veiculo = anterior?.VeiculoId == dados.Veiculo.Id ? anterior.Veiculo : VeiculoContratado.Capturar(dados.Veiculo, dados.Grupo),
            Plano = anterior?.PlanoId == dados.Plano.Id ? anterior.Plano : PlanoContratado.Capturar(dados.Plano),
        };
        foreach (var atual in atuais)
        {
            var mantido = anterior?.Itens.SingleOrDefault(item => item.Categoria == atual.Categoria && item.OrigemId == atual.OrigemId);
            revisao._itens.Add(ItemContratado.Capturar(empresaId, revisao.Id, revisao.Numero, atual, mantido));
        }
        revisao.Estimativa = DemonstrativoEstimado.Calcular(empresaId, revisao.Id, saida,
            revisao.RetornoPrevistoUtc, agora, revisao.PlanoId, revisao.Plano.Parametros(),
            revisao.Itens.Select(item => item.ParaCalculo()).ToArray());
        return revisao;
    }
}
