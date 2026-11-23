using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Locadora.Api.Host;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Api.Modulos.FrotaPrecos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaGrupo
{
    [Required, MaxLength(200)] public string? Nome { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EntradaPrecosPlano
{
    [Required] public string? PrecoDiario { get; init; }
    public string? PrecoKm { get; init; }
    public int? FranquiaDiariaKm { get; init; }
    public string? PrecoKmExcedente { get; init; }
    public PrecosPlano ParaDominio() => new(DecimalTransporte.Ler(PrecoDiario, "precoDiario"),
        DecimalTransporte.Opcional(PrecoKm, "precoKm"), FranquiaDiariaKm, DecimalTransporte.Opcional(PrecoKmExcedente, "precoKmExcedente"));
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaPlano : EntradaPrecosPlano
{
    public Guid GrupoId { get; init; }
    [Required] public string? Modalidade { get; init; }
    public ModalidadePlano LerModalidade() => Modalidade switch
    {
        "Diario" => ModalidadePlano.Diario,
        "Controlado" => ModalidadePlano.Controlado,
        "Livre" => ModalidadePlano.Livre,
        _ => throw new ValidacaoCadastroException("modalidade", "Selecione Diário, Controlado ou Livre."),
    };
}

public sealed record SaidaPlano(Guid Id, Guid GrupoId, string Modalidade, string PrecoDiario,
    string? PrecoKm, int? FranquiaDiariaKm, string? PrecoKmExcedente)
{
    public static SaidaPlano De(Plano plano) => new(plano.Id, plano.GrupoId, plano.Modalidade.ToString(),
        DecimalTransporte.Escrever(plano.PrecoDiario),
        plano.PrecoKm is decimal km ? DecimalTransporte.Escrever(km) : null,
        plano.FranquiaDiariaKm, plano.PrecoKmExcedente is decimal excedente ? DecimalTransporte.Escrever(excedente) : null);
}

public sealed record SaidaGrupo(Guid Id, string Nome, IReadOnlyList<SaidaPlano> Planos);
