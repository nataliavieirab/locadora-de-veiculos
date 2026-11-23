using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Locadora.Domain.Modulos.Pessoas;

namespace Locadora.Api.Modulos.Pessoas;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(EntradaClientePf), "PF")]
[JsonDerivedType(typeof(EntradaClientePj), "PJ")]
public abstract class EntradaCliente
{
    [Required, MaxLength(200)] public string? Nome { get; init; }
    [Required, MaxLength(32)] public string? Telefone { get; init; }
    [MaxLength(254)] public string? Email { get; init; }
    [Required] public EntradaEndereco? Endereco { get; init; }
    public abstract DadosCliente ParaDominio();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaClientePf : EntradaCliente
{
    [Required, MaxLength(64)] public string? Cpf { get; init; }
    [Required, MaxLength(64)] public string? Rg { get; init; }
    [Required, MaxLength(64)] public string? Cnh { get; init; }
    public override DadosCliente ParaDominio() =>
        new(TipoCliente.PF, Nome!, Cpf, null, Rg, Cnh, Telefone!, Endereco!.ParaDominio(), Email);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaClientePj : EntradaCliente
{
    [Required, MaxLength(64)] public string? Cnpj { get; init; }
    public override DadosCliente ParaDominio() =>
        new(TipoCliente.PJ, Nome!, null, Cnpj, null, null, Telefone!, Endereco!.ParaDominio(), Email);
}

public sealed class EntradaEndereco
{
    [Required, MaxLength(100)] public string? Estado { get; init; }
    [Required, MaxLength(100)] public string? Cidade { get; init; }
    [Required, MaxLength(100)] public string? Bairro { get; init; }
    [Required, MaxLength(200)] public string? Rua { get; init; }
    [Required, MaxLength(200)] public string? Numero { get; init; }
    public Endereco ParaDominio() => new(Estado!, Cidade!, Bairro!, Rua!, Numero!);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaCondutor
{
    public Guid ClienteId { get; init; }
    [Required, MaxLength(200)] public string? Nome { get; init; }
    [Required, MaxLength(254)] public string? Email { get; init; }
    [Required, MaxLength(64)] public string? Cpf { get; init; }
    [Required, MaxLength(64)] public string? Cnh { get; init; }
    [Required] public DateOnly? ValidadeCnh { get; init; }
    [Required, MaxLength(32)] public string? Telefone { get; init; }
    public bool ProprioCliente { get; init; }
    public DadosCondutor ParaDominio() => new(ClienteId, Nome!, Email!, Cpf!, Cnh!, ValidadeCnh!.Value, Telefone!);
}

public sealed record EntradaVinculo(Guid OutroClienteId);
public sealed record SaidaEndereco(string Estado, string Cidade, string Bairro, string Rua, string Numero);

public sealed record ResumoCliente(Guid Id, string Tipo, string Nome, string Telefone,
    string? Email, string? Cpf, string? Cnpj, string? Rg, string? Cnh);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(SaidaClientePf), "PF")]
[JsonDerivedType(typeof(SaidaClientePj), "PJ")]
public abstract record SaidaCliente(Guid Id, string Nome, string Telefone, string? Email, SaidaEndereco Endereco);

public sealed record SaidaClientePf(Guid Id, string Nome, string Telefone, string? Email,
    SaidaEndereco Endereco, string Cpf, string Rg, string Cnh) : SaidaCliente(Id, Nome, Telefone, Email, Endereco);

public sealed record SaidaClientePj(Guid Id, string Nome, string Telefone, string? Email,
    SaidaEndereco Endereco, string Cnpj) : SaidaCliente(Id, Nome, Telefone, Email, Endereco);

public sealed record SaidaCondutor(Guid Id, Guid ClienteId, string ClienteNome, string Nome, string Email,
    string Cpf, string Cnh, DateOnly ValidadeCnh, string Telefone);

public sealed record SaidaVinculo(Guid Id, Guid ClientePfId, string ClientePfNome, Guid ClientePjId, string ClientePjNome);

internal static class SaidasPessoas
{
    public static SaidaCliente Cliente(Cliente cliente)
    {
        var endereco = new SaidaEndereco(cliente.Endereco.Estado, cliente.Endereco.Cidade,
            cliente.Endereco.Bairro, cliente.Endereco.Rua, cliente.Endereco.Numero);
        return cliente.Tipo == TipoCliente.PF
            ? new SaidaClientePf(cliente.Id, cliente.Nome, cliente.Telefone, cliente.Email, endereco, cliente.Cpf!, cliente.Rg!, cliente.Cnh!)
            : new SaidaClientePj(cliente.Id, cliente.Nome, cliente.Telefone, cliente.Email, endereco, cliente.Cnpj!);
    }

    public static ResumoCliente Resumo(Cliente cliente) => new(cliente.Id, cliente.Tipo.ToString(), cliente.Nome,
        cliente.Telefone, cliente.Email, cliente.Cpf, cliente.Cnpj, cliente.Rg, cliente.Cnh);

    public static SaidaCondutor Condutor(Condutor condutor, Cliente cliente) => new(condutor.Id, cliente.Id,
        cliente.Nome, condutor.Nome, condutor.Email, condutor.Cpf, condutor.Cnh, condutor.ValidadeCnh, condutor.Telefone);
}
