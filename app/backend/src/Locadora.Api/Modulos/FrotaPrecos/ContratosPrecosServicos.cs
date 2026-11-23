using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Locadora.Api.Host;
using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Api.Modulos.FrotaPrecos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaTaxaServico
{
    [Required, MaxLength(200)] public string? Nome { get; init; }
    [Required] public string? Preco { get; init; }
    [Required] public string? Modalidade { get; init; }

    public DadosTaxaServico ParaDominio() => new(Nome!, DecimalTransporte.Ler(Preco, "preco"), Modalidade switch
    {
        "Fixa" => ModalidadeTaxa.Fixa,
        "Diaria" => ModalidadeTaxa.Diaria,
        _ => throw new ValidacaoCadastroException("modalidade", "Selecione Fixa ou Diária."),
    });
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaPrecoSeguro
{
    [Required] public string? PrecoDiario { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaPrecoCombustivel
{
    [Required] public string? PrecoUnitario { get; init; }
}

public sealed record SaidaTaxaServico(Guid Id, string Nome, string Preco, string Modalidade)
{
    public static SaidaTaxaServico De(TaxaServico taxa) =>
        new(taxa.Id, taxa.Nome, DecimalTransporte.Escrever(taxa.Preco), taxa.Modalidade.ToString());
}

public sealed record SaidaPrecoSeguro(Guid? Id, string Tipo, bool Configurado, string? PrecoDiario)
{
    public static SaidaPrecoSeguro De(TipoSeguro tipo, PrecoSeguro? seguro) =>
        new(seguro?.Id, tipo.ToString(), seguro is not null,
            seguro is null ? null : DecimalTransporte.Escrever(seguro.PrecoDiario));
}

public sealed record SaidaVigenciaCombustivel(Guid Id, string PrecoUnitario, DateTimeOffset InicioVigenciaUtc)
{
    public static SaidaVigenciaCombustivel De(PrecoCombustivel preco) =>
        new(preco.Id, DecimalTransporte.Escrever(preco.PrecoUnitario), preco.InicioVigenciaUtc);
}

public sealed record SaidaPrecoCombustivel(string Tipo, bool Configurado, SaidaVigenciaCombustivel? Atual,
    IReadOnlyList<SaidaVigenciaCombustivel> Historico);

public static class TiposConfiguracao
{
    public static TipoSeguro Seguro(string tipo) => tipo switch
    {
        "Cliente" => TipoSeguro.Cliente,
        "Terceiros" => TipoSeguro.Terceiros,
        _ => throw new ValidacaoCadastroException("tipo", "Selecione Cliente ou Terceiros."),
    };

    public static TipoCombustivel Combustivel(string tipo) => tipo switch
    {
        "Gasolina" => TipoCombustivel.Gasolina,
        "Gas" => TipoCombustivel.Gas,
        "Diesel" => TipoCombustivel.Diesel,
        "Alcool" => TipoCombustivel.Alcool,
        _ => throw new ValidacaoCadastroException("tipo", "Selecione Gasolina, Gás, Diesel ou Álcool."),
    };
}
