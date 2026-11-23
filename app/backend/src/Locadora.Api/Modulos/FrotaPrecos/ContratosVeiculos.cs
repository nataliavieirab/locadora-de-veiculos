using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Locadora.Api.Host;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Api.Modulos.FrotaPrecos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaVeiculo
{
    [Required, MaxLength(16)] public string? Placa { get; init; }
    [Required, MaxLength(100)] public string? Marca { get; init; }
    [Required, MaxLength(100)] public string? Cor { get; init; }
    [Required, MaxLength(100)] public string? Modelo { get; init; }
    [Required] public string? Combustivel { get; init; }
    [Required] public string? CapacidadeLitros { get; init; }
    public int Ano { get; init; }
    public Guid GrupoId { get; init; }

    public DadosVeiculo ParaDominio() => new(Placa!, Marca!, Cor!, Modelo!, Combustivel switch
    {
        "Gasolina" => TipoCombustivel.Gasolina,
        "Gas" => TipoCombustivel.Gas,
        "Diesel" => TipoCombustivel.Diesel,
        "Alcool" => TipoCombustivel.Alcool,
        _ => throw new ValidacaoCadastroException("combustivel", "Selecione Gasolina, Gás, Diesel ou Álcool."),
    }, DecimalTransporte.Ler(CapacidadeLitros, "capacidadeLitros"), Ano, GrupoId);
}

public sealed record SaidaVeiculo(Guid Id, string Placa, string Marca, string Cor, string Modelo,
    string Combustivel, string CapacidadeLitros, int Ano, Guid GrupoId)
{
    public static SaidaVeiculo De(Veiculo veiculo) => new(veiculo.Id, veiculo.Placa, veiculo.Marca,
        veiculo.Cor, veiculo.Modelo, veiculo.Combustivel.ToString(),
        DecimalTransporte.Escrever(veiculo.CapacidadeLitros), veiculo.Ano, veiculo.GrupoId);
}
