using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.FrotaPrecos;

public enum ModalidadePlano { Diario, Controlado, Livre }

public sealed class Plano : CadastroExcluivel
{
    public Guid GrupoId { get; private set; }
    public ModalidadePlano Modalidade { get; private set; }
    public decimal PrecoDiario { get; private set; }
    public decimal? PrecoKm { get; private set; }
    public int? FranquiaDiariaKm { get; private set; }
    public decimal? PrecoKmExcedente { get; private set; }

    private Plano()
    {
    }

    private Plano(Guid empresaId, Guid grupoId, ModalidadePlano modalidade, DateTimeOffset agora)
        : base(empresaId, agora)
    {
        GrupoId = ValidacaoCadastro.Referencia(grupoId, "grupoId");
        Modalidade = ValidacaoCadastro.Modalidade(modalidade, "modalidade");
    }

    public static Plano Novo(Guid empresaId, Guid grupoId, ModalidadePlano modalidade,
        decimal precoDiario, decimal? precoKm, int? franquiaDiariaKm, decimal? precoKmExcedente, DateTimeOffset agora)
    {
        var plano = new Plano(empresaId, grupoId, modalidade, agora);
        plano.Alterar(precoDiario, precoKm, franquiaDiariaKm, precoKmExcedente, agora);
        return plano;
    }

    public void Alterar(decimal precoDiario, decimal? precoKm, int? franquiaDiariaKm,
        decimal? precoKmExcedente, DateTimeOffset agora)
    {
        ExigirAtivo();
        ValidacaoCadastro.Preco(precoDiario, "precoDiario");
        if (Modalidade == ModalidadePlano.Diario)
        {
            if (precoKm is null)
            {
                throw new ValidacaoCadastroException("precoKm", "Informe o preço por quilômetro.");
            }
            ValidacaoCadastro.Preco(precoKm.Value, "precoKm");
            if (franquiaDiariaKm is not null || precoKmExcedente is not null)
            {
                throw new ValidacaoCadastroException("modalidade", "O plano Diário não admite franquia ou preço excedente.");
            }
        }
        else if (Modalidade == ModalidadePlano.Controlado)
        {
            if (franquiaDiariaKm is null or < 0)
            {
                throw new ValidacaoCadastroException("franquiaDiariaKm", "Informe uma franquia inteira não negativa.");
            }
            if (precoKmExcedente is null)
            {
                throw new ValidacaoCadastroException("precoKmExcedente", "Informe o preço por quilômetro excedente.");
            }
            ValidacaoCadastro.Preco(precoKmExcedente.Value, "precoKmExcedente");
            if (precoKm is not null)
            {
                throw new ValidacaoCadastroException("precoKm", "O plano Controlado usa somente o preço por quilômetro excedente.");
            }
        }
        else if (precoKm is not null || franquiaDiariaKm is not null || precoKmExcedente is not null)
        {
            throw new ValidacaoCadastroException("modalidade", "O plano Livre admite somente o preço diário.");
        }

        RegistrarAlteracao(agora);
        PrecoDiario = precoDiario;
        PrecoKm = precoKm;
        FranquiaDiariaKm = franquiaDiariaKm;
        PrecoKmExcedente = precoKmExcedente;
    }
}
