using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public sealed record ComandoContratacao(Guid ClienteId, Guid CondutorId, Guid VeiculoId, Guid PlanoId,
    DateTimeOffset RetornoPrevisto, int HodometroInicial, IReadOnlyList<Guid> TaxasServicoIds, IReadOnlyList<TipoSeguro> Seguros)
{
    public void Validar()
    {
        ValidacaoCadastro.Referencia(ClienteId, "clienteId");
        ValidacaoCadastro.Referencia(CondutorId, "condutorId");
        ValidacaoCadastro.Referencia(VeiculoId, "veiculoId");
        ValidacaoCadastro.Referencia(PlanoId, "planoId");
        if (HodometroInicial < 0)
            throw new ValidacaoCadastroException("hodometroInicial", "Informe um hodômetro inteiro não negativo.");
        if (TaxasServicoIds is null || Seguros is null)
            throw new ValidacaoCadastroException("itens", "Informe as listas de adicionais, mesmo quando vazias.");
        if (TaxasServicoIds.Distinct().Count() != TaxasServicoIds.Count || Seguros.Distinct().Count() != Seguros.Count)
            throw new ValidacaoCadastroException("itens", "Não repita taxas ou seguros.");
        foreach (var id in TaxasServicoIds) ValidacaoCadastro.Referencia(id, "taxasServicoIds");
        foreach (var tipo in Seguros) ValidacaoCadastro.Modalidade(tipo, "seguros");
    }
}
