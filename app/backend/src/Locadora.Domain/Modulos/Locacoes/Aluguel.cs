using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public enum EstadoAluguel { EmAberto = 0, Concluido = 1 }

public sealed record DadosDevolucao(DateTimeOffset RetornoReal, int HodometroFinal, decimal LitrosRestantes,
    PrecoCombustivel Combustivel, IReadOnlyCollection<TaxaServico> TaxasAdicionais);

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
    public DateTimeOffset? ExcluidoEmUtc { get; private set; }
    public DemonstrativoFinal? Devolucao { get; private set; }
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

    public ResultadoCalculoFinal CalcularDevolucao(DadosDevolucao dados)
    {
        if (Estado != EstadoAluguel.EmAberto)
            throw new ValidacaoCadastroException("estado", "Somente um contrato Em aberto admite devolução.");
        if (dados.Combustivel.EmpresaId != EmpresaId)
            throw new EmpresaDivergenteException();
        var revisao = RevisaoAtual;
        var adicionais = revisao.Itens.Select(item => new AdicionalCalculoFinal(item.ParaCalculo(), item.OrigemInclusao)).ToList();
        foreach (var taxa in dados.TaxasAdicionais)
        {
            if (taxa.EmpresaId != EmpresaId)
                throw new EmpresaDivergenteException();
            if (taxa.ExcluidoEmUtc is not null)
                throw new ValidacaoCadastroException("taxasAdicionaisIds", "Selecione taxas ativas da empresa.");
            adicionais.Add(new(new(CategoriaItem.TaxaServico, taxa.Id, null, taxa.Nome, taxa.Modalidade, taxa.Preco),
                OrigemInclusao.Devolucao));
        }
        return CalculadoraDevolucao.Calcular(new(SaidaUtc, revisao.RetornoPrevistoUtc, dados.RetornoReal,
            revisao.HodometroInicial, dados.HodometroFinal, revisao.PlanoId, revisao.Plano.Parametros(), revisao.Veiculo.Combustivel,
            revisao.Veiculo.CapacidadeLitros, dados.LitrosRestantes,
            new(dados.Combustivel.Id, dados.Combustivel.Tipo, dados.Combustivel.InicioVigenciaUtc, dados.Combustivel.PrecoUnitario),
            adicionais));
    }

    public DemonstrativoFinal Concluir(DadosDevolucao dados, DateTimeOffset agora)
    {
        var calculo = CalcularDevolucao(dados);
        var final = DemonstrativoFinal.Capturar(RevisaoAtual, calculo, dados.TaxasAdicionais, agora);
        // Todas as validações precedem a transição; o domínio não simula rowversion nem disponibilidade.
        RegistrarAlteracao(agora);
        Devolucao = final;
        Estado = EstadoAluguel.Concluido;
        return final;
    }

    public bool Ocultar(DateTimeOffset agora)
    {
        if (Estado != EstadoAluguel.Concluido || Devolucao is null)
            throw new ValidacaoCadastroException("estado", "Somente um contrato concluído pode ser ocultado.");
        if (ExcluidoEmUtc is not null) return false;
        RegistrarAlteracao(agora);
        ExcluidoEmUtc = AlteradoEmUtc;
        return true;
    }

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
