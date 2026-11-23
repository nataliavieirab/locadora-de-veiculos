using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.FrotaPrecos;

public enum TipoSeguro { Cliente, Terceiros }

public sealed class PrecoSeguro : EntidadeCadastro
{
    public TipoSeguro Tipo { get; private set; }
    public decimal PrecoDiario { get; private set; }

    private PrecoSeguro()
    {
    }

    private PrecoSeguro(Guid empresaId, TipoSeguro tipo, decimal precoDiario, DateTimeOffset agora)
        : base(empresaId, agora)
    {
        Tipo = ValidacaoCadastro.Modalidade(tipo, "tipo");
        PrecoDiario = ValidacaoCadastro.Preco(precoDiario, "precoDiario");
    }

    public static PrecoSeguro Novo(Guid empresaId, TipoSeguro tipo, decimal precoDiario, DateTimeOffset agora) =>
        new(empresaId, tipo, precoDiario, agora);

    public void Alterar(decimal precoDiario, DateTimeOffset agora)
    {
        ValidacaoCadastro.Preco(precoDiario, "precoDiario");
        RegistrarAlteracao(agora);
        PrecoDiario = precoDiario;
    }
}
