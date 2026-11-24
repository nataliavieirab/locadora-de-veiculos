using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Locadora.Api.Host;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Api.Modulos.Locacoes;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EntradaEstimativa : EntradaContratacao
{
    public Guid? AluguelId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EntradaContratacao
{
    public Guid ClienteId { get; init; }
    public Guid CondutorId { get; init; }
    public Guid VeiculoId { get; init; }
    public Guid PlanoId { get; init; }
    [Required] public string? RetornoPrevisto { get; init; }
    [Required] public int? HodometroInicial { get; init; }
    [Required] public Guid[]? TaxasServicoIds { get; init; }
    [Required] public string[]? Seguros { get; init; }

    public ComandoContratacao ParaComando() => new(ClienteId, CondutorId, VeiculoId, PlanoId,
        TempoLocacao.NormalizarHorarioSaoPaulo(InstanteTransporte.Ler(RetornoPrevisto, "retornoPrevisto")),
        HodometroInicial ?? throw new ValidacaoCadastroException("hodometroInicial", "Informe o hodômetro inicial."),
        TaxasServicoIds ?? throw new ValidacaoCadastroException("taxasServicoIds", "Informe as taxas ou uma lista vazia."),
        (Seguros ?? throw new ValidacaoCadastroException("seguros", "Informe os seguros ou uma lista vazia.")).Select(tipo => tipo switch
        {
            "Cliente" => TipoSeguro.Cliente,
            "Terceiros" => TipoSeguro.Terceiros,
            _ => throw new ValidacaoCadastroException("seguros", "Selecione um tipo de seguro válido."),
        }).ToArray());
}

public static partial class InstanteTransporte
{
    public static DateTimeOffset Ler(string? texto, string campo)
    {
        if (texto is null || !Formato().IsMatch(texto)
            || !DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instante))
            throw new ValidacaoCadastroException(campo, "Informe data e hora ISO 8601 com offset explícito.");
        return instante;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}

public sealed record SaidaItemListaAluguel(Guid Id, string Estado, int Revisao, string CondutorNome,
    string CondutorCpfResumo, string VeiculoPlaca, string PlanoModalidade, DateTimeOffset SaidaUtc,
    DateTimeOffset RetornoPrevistoUtc, string TotalConhecido)
{
    public static SaidaItemListaAluguel De(ItemListaAluguel item) => new(item.Id, item.Estado.ToString(), item.Revisao,
        item.CondutorNome, item.CondutorCpfResumo, item.VeiculoPlaca, item.PlanoModalidade, item.SaidaUtc,
        item.RetornoPrevistoUtc, DecimalTransporte.Escrever(item.TotalConhecido));
}

public sealed record SaidaContrato(Guid Id, string Estado, int Revisao, Guid ClienteId, Guid CondutorId,
    Guid VeiculoId, Guid PlanoId, DateTimeOffset SaidaUtc, DateTimeOffset RetornoPrevistoUtc, int HodometroInicial,
    string Caucao, SaidaClienteContratado Cliente, SaidaCondutorContratado Condutor, SaidaVeiculoContratado Veiculo,
    SaidaPlanoContratado Plano, IReadOnlyList<SaidaItemContratado> Itens, SaidaEstimativa Estimativa)
{
    public static SaidaContrato De(RevisaoContrato r) => new(r.AluguelId, "EmAberto", r.Numero,
        r.ClienteId, r.CondutorId, r.VeiculoId, r.PlanoId, r.SaidaUtc, r.RetornoPrevistoUtc, r.HodometroInicial,
        "1000.00", new(r.Cliente.Tipo.ToString(), r.Cliente.Nome, r.Cliente.Cpf, r.Cliente.Cnpj,
            r.Cliente.Rg, r.Cliente.Cnh, r.Cliente.Telefone, r.Cliente.Email,
            new(r.Cliente.Endereco.Estado, r.Cliente.Endereco.Cidade, r.Cliente.Endereco.Bairro,
                r.Cliente.Endereco.Rua, r.Cliente.Endereco.Numero)),
        new(r.Condutor.Nome, r.Condutor.Cpf, r.Condutor.Cnh, r.Condutor.ValidadeCnh, r.Condutor.Email, r.Condutor.Telefone),
        new(r.Veiculo.Placa, r.Veiculo.Marca, r.Veiculo.Cor, r.Veiculo.Modelo, r.Veiculo.Combustivel.ToString(),
            DecimalTransporte.Escrever(r.Veiculo.CapacidadeLitros), r.Veiculo.Ano, r.Veiculo.GrupoId, r.Veiculo.GrupoNome),
        new(r.Plano.Modalidade.ToString(), DecimalTransporte.Escrever(r.Plano.PrecoDiario),
            r.Plano.PrecoKm is decimal km ? DecimalTransporte.Escrever(km) : null, r.Plano.FranquiaDiariaKm,
            r.Plano.PrecoKmExcedente is decimal excedente ? DecimalTransporte.Escrever(excedente) : null),
        r.Itens.OrderBy(i => i.Categoria).ThenBy(i => i.OrigemId).Select(i => new SaidaItemContratado(
            i.Categoria.ToString(), i.OrigemId, i.TipoSeguro?.ToString(), i.Nome, i.Modalidade.ToString(),
            DecimalTransporte.Escrever(i.PrecoUnitario), i.OrigemInclusao.ToString(), i.RevisaoPrimeiraInclusao)).ToArray(),
        SaidaEstimativa.De(r.Estimativa));
}

public sealed record SaidaEnderecoContratado(string Estado, string Cidade, string Bairro, string Rua, string Numero);
public sealed record SaidaClienteContratado(string Tipo, string Nome, string? Cpf, string? Cnpj, string? Rg,
    string? Cnh, string Telefone, string? Email, SaidaEnderecoContratado Endereco);
public sealed record SaidaCondutorContratado(string Nome, string Cpf, string Cnh, DateOnly ValidadeCnh, string Email, string Telefone);
public sealed record SaidaVeiculoContratado(string Placa, string Marca, string Cor, string Modelo, string Combustivel,
    string CapacidadeLitros, int Ano, Guid GrupoId, string GrupoNome);
public sealed record SaidaPlanoContratado(string Modalidade, string PrecoDiario, string? PrecoKm, int? FranquiaDiariaKm, string? PrecoKmExcedente);
public sealed record SaidaItemContratado(string Categoria, Guid OrigemId, string? TipoSeguro, string Nome,
    string Modalidade, string PrecoUnitario, string OrigemInclusao, int RevisaoPrimeiraInclusao);
public sealed record SaidaComponenteEstimativa(string? Categoria, Guid OrigemId, string? TipoSeguro, string Nome,
    string PrecoUnitario, int Multiplicador, string ValorBruto, string ValorArredondado);
public sealed record SaidaEstimativa(string VersaoRegra, DateTimeOffset CalculadoEmUtc, DateTimeOffset SaidaUtc,
    DateTimeOffset RetornoPrevistoUtc, int Diarias, int? Quilometros, long? FranquiaTotalKm, bool QuilometragemPendente,
    bool CombustivelPendente, bool AtrasoPendente, string TermoDiariasBruto, string TermoKmBruto,
    IReadOnlyList<SaidaComponenteEstimativa> Componentes, string TotalConhecido, string Caucao)
{
    public static SaidaEstimativa De(DemonstrativoEstimado e) => new(e.VersaoRegra, e.CalculadoEmUtc, e.SaidaUtc,
        e.RetornoPrevistoUtc, e.Diarias, e.Quilometros, e.FranquiaTotalKm, e.QuilometragemPendente,
        e.CombustivelPendente, e.AtrasoPendente, Bruto(e.TermoDiariasBruto), Bruto(e.TermoKmBruto),
        e.Componentes.OrderBy(c => c.Ordem).Select(c => new SaidaComponenteEstimativa(c.Categoria?.ToString(),
            c.OrigemId, c.TipoSeguro?.ToString(), c.Nome, DecimalTransporte.Escrever(c.PrecoUnitario),
            c.Multiplicador, Bruto(c.ValorBruto), c.ValorArredondado.ToString("0.00", CultureInfo.InvariantCulture))).ToArray(),
        e.TotalConhecido.ToString("0.00", CultureInfo.InvariantCulture), "1000.00");

    private static string Bruto(decimal valor) => valor.ToString("0.00#######", CultureInfo.InvariantCulture);
}
