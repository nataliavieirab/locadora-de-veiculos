using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.FrotaPrecos;

public sealed class PrecoCombustivel : IEntidadeEmpresa
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public TipoCombustivel Tipo { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public DateTimeOffset InicioVigenciaUtc { get; private set; }
    public long Ordem { get; private set; }

    private PrecoCombustivel()
    {
    }

    public static PrecoCombustivel Novo(Guid empresaId, TipoCombustivel tipo, decimal precoUnitario, DateTimeOffset agora)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("Informe a empresa da vigência.", nameof(empresaId));
        }

        return new PrecoCombustivel
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            Tipo = ValidacaoCadastro.Modalidade(tipo, "tipo"),
            PrecoUnitario = ValidacaoCadastro.Preco(precoUnitario, "precoUnitario"),
            InicioVigenciaUtc = agora.ToUniversalTime(),
        };
    }
}
