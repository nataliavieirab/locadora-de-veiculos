using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public enum EstadoAluguel { EmAberto }

public sealed class Aluguel : EntidadeCadastro
{
    private readonly List<RevisaoContrato> _revisoes = [];
    public EstadoAluguel Estado { get; private set; }
    public Guid ClienteId { get; private set; }
    public Guid CondutorId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public Guid PlanoId { get; private set; }
    public DateTimeOffset SaidaUtc { get; private set; }
    public DateTimeOffset RetornoPrevistoUtc { get; private set; }
    public int HodometroInicial { get; private set; }
    public int NumeroRevisaoAtual { get; private set; }
    public IReadOnlyCollection<RevisaoContrato> Revisoes => _revisoes.AsReadOnly();
    public RevisaoContrato RevisaoAtual => _revisoes.Single(revisao => revisao.Numero == NumeroRevisaoAtual);

    private Aluguel() { }

    private Aluguel(Guid empresaId, DateTimeOffset agora) : base(empresaId, agora)
    {
        SaidaUtc = agora.ToUniversalTime();
        Estado = EstadoAluguel.EmAberto;
    }

    public static Aluguel Criar(Guid empresaId, DadosContratacao dados, DateTimeOffset agora)
    {
        var aluguel = new Aluguel(empresaId, agora);
        aluguel.ConfirmarRevisao(RevisaoContrato.Preparar(empresaId, aluguel.Id, aluguel.SaidaUtc, dados, agora), agora);
        return aluguel;
    }

    public RevisaoContrato PrepararEdicao(DadosContratacao dados, DateTimeOffset agora)
    {
        if (Estado != EstadoAluguel.EmAberto)
            throw new ValidacaoCadastroException("estado", "O contrato não está Em aberto e não admite edição.");
        return RevisaoContrato.Preparar(EmpresaId, Id, SaidaUtc, dados, agora, RevisaoAtual);
    }

    public void Editar(DadosContratacao dados, DateTimeOffset agora)
        => ConfirmarRevisao(PrepararEdicao(dados, agora), agora);

    private void ConfirmarRevisao(RevisaoContrato revisao, DateTimeOffset agora)
    {
        RegistrarAlteracao(agora);
        _revisoes.Add(revisao);
        ClienteId = revisao.ClienteId;
        CondutorId = revisao.CondutorId;
        VeiculoId = revisao.VeiculoId;
        PlanoId = revisao.PlanoId;
        RetornoPrevistoUtc = revisao.RetornoPrevistoUtc;
        HodometroInicial = revisao.HodometroInicial;
        NumeroRevisaoAtual = revisao.Numero;
    }
}
