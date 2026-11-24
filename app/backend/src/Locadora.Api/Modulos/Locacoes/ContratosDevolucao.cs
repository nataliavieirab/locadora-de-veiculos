using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using Locadora.Api.Host;
using Locadora.Application.Modulos.Locacoes;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Api.Modulos.Locacoes;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EntradaDevolucao
{
    [Required] public string? RetornoReal { get; init; }
    [Required] public int? HodometroFinal { get; init; }
    [Required] public string? LitrosRestantes { get; init; }
    [Required] public Guid[]? TaxasAdicionaisIds { get; init; }

    public ComandoDevolucao ParaComando()
    {
        if (LitrosRestantes is { } litros && litros.Contains('.')
            && litros.Length - litros.IndexOf('.') - 1 > 3)
            throw new ValidacaoCadastroException("litrosRestantes", "Informe litros com até três casas decimais.");
        var comando = new ComandoDevolucao(InstanteTransporte.Ler(RetornoReal, "retornoReal").ToUniversalTime(),
            HodometroFinal ?? throw new ValidacaoCadastroException("hodometroFinal", "Informe o hodômetro final."),
            DecimalTransporte.Ler(LitrosRestantes, "litrosRestantes"),
            TaxasAdicionaisIds ?? throw new ValidacaoCadastroException("taxasAdicionaisIds", "Informe as taxas ou uma lista vazia."));
        comando.Validar();
        return comando;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaConclusaoDevolucao : EntradaDevolucao
{
    [Required] public EntradaVersoesDevolucao? VersoesAceitas { get; init; }
    public ComandoConclusaoDevolucao ParaConclusao() => new(ParaComando(),
        VersoesAceitas?.ParaAceite() ?? throw new ValidacaoCadastroException("versoesAceitas", "Aceite uma prévia atual."));
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaVersoesDevolucao
{
    public Guid RevisaoId { get; init; }
    [Required] public string? VersaoRegra { get; init; }
    public Guid CombustivelId { get; init; }
    [Required] public EntradaVersaoTaxa[]? TaxasNovas { get; init; }
    [Required] public string? ResumoCalculo { get; init; }

    public VersoesDevolucaoAceitas ParaAceite() => new(RevisaoId, VersaoRegra!, CombustivelId,
        (TaxasNovas ?? []).Select(t => t is null
            ? throw new ValidacaoCadastroException("versoesAceitas", "Informe versões válidas das taxas.")
            : new VersaoTaxaAceita(t.Id, t.Versao!)).ToArray(), ResumoCalculo!);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntradaVersaoTaxa
{
    public Guid Id { get; init; }
    [Required] public string? Versao { get; init; }
}

public sealed record SaidaCombustivelDevolucao(Guid Id, string Tipo, DateTimeOffset InicioVigenciaUtc, string PrecoUnitario);
public sealed record SaidaTaxaDevolucao(Guid Id, string Versao, string Nome, string Modalidade,
    string PrecoUnitario, string OrigemInclusao);
public sealed record SaidaComponenteFinal(string Categoria, Guid? OrigemId, string? TipoSeguro, string Nome,
    string? Modalidade, string? OrigemInclusao, string Quantidade, string PrecoUnitario,
    string ValorBruto, string ValorArredondado)
{
    internal static string Bruto(decimal valor) => valor.ToString("0.00#######", CultureInfo.InvariantCulture);
    internal static string Dinheiro(decimal valor) => valor.ToString("0.00", CultureInfo.InvariantCulture);
    public static SaidaComponenteFinal De(ComponenteFinalCalculado c) => new(c.Categoria.ToString(), c.OrigemId,
        c.TipoSeguro?.ToString(), c.Nome, c.Modalidade?.ToString(), c.OrigemInclusao?.ToString(),
        DecimalTransporte.Escrever(c.Quantidade), DecimalTransporte.Escrever(c.PrecoUnitario),
        Bruto(c.ValorBruto), Dinheiro(c.ValorArredondado));
}

public sealed record SaidaApuracaoDevolucao(string VersaoRegra, DateTimeOffset SaidaUtc,
    DateTimeOffset RetornoPrevistoUtc, DateTimeOffset RetornoRealUtc, int HodometroInicial, int HodometroFinal,
    string LitrosRestantes, string CapacidadeLitros, string LitrosRepostos, int Diarias, int Quilometros,
    long? FranquiaTotalKm, long? ExcedenteKm, bool EmAtraso, string TermoDiariasBruto, string TermoKmBruto,
    SaidaCombustivelDevolucao Combustivel, IReadOnlyList<SaidaTaxaDevolucao> TaxasNovas,
    IReadOnlyList<SaidaComponenteFinal> Componentes, string TotalFinal, string Caucao)
{
    public static SaidaApuracaoDevolucao De(ResultadoCalculoFinal c, IReadOnlyList<SaidaTaxaDevolucao> taxas)
        => new(ResultadoCalculoFinal.VersaoRegra, c.Entradas.Saida, c.Entradas.RetornoPrevisto,
            c.Entradas.RetornoReal, c.Entradas.HodometroInicial, c.Entradas.HodometroFinal,
            DecimalTransporte.Escrever(c.Entradas.LitrosRestantes), DecimalTransporte.Escrever(c.Entradas.CapacidadeLitros),
            DecimalTransporte.Escrever(c.LitrosRepostos), c.Diarias, c.Quilometros, c.FranquiaTotalKm, c.ExcedenteKm,
            c.EmAtraso, SaidaComponenteFinal.Bruto(c.TermoDiariasBruto), SaidaComponenteFinal.Bruto(c.TermoKmBruto),
            new(c.Entradas.Combustivel!.Id, c.Entradas.TipoCombustivel.ToString(),
                c.Entradas.Combustivel.InicioVigenciaUtc, DecimalTransporte.Escrever(c.Entradas.Combustivel.PrecoUnitario)),
            taxas, c.Componentes.Select(SaidaComponenteFinal.De).ToArray(),
            SaidaComponenteFinal.Dinheiro(c.TotalFinal), "1000.00");
}

public sealed record SaidaPreviaDevolucao(Guid AluguelId, Guid RevisaoId, int Revisao, string VersaoAluguel,
    SaidaApuracaoDevolucao Calculo, VersoesDevolucaoAceitas VersoesAceitas)
{
    public static SaidaPreviaDevolucao De(ResultadoPreviaDevolucao r) => new(r.AluguelId, r.RevisaoId, r.Revisao,
        VersaoCadastro.Etag(r.RowVersion), SaidaApuracaoDevolucao.De(r.Calculo,
            r.Calculo.Entradas.Adicionais.Where(a => a.OrigemInclusao == OrigemInclusao.Devolucao)
                .OrderBy(a => a.Item.OrigemId).Select(a => new SaidaTaxaDevolucao(a.Item.OrigemId,
                    r.VersoesAceitas.TaxasNovas.Single(t => t.Id == a.Item.OrigemId).Versao, a.Item.Nome,
                    a.Item.Modalidade.ToString(), DecimalTransporte.Escrever(a.Item.PrecoUnitario), "Devolucao")).ToArray()),
        r.VersoesAceitas);
}

public sealed record SaidaFinalDevolucao(Guid Id, Guid RevisaoId, DateTimeOffset CalculadoEmUtc,
    DateTimeOffset RegistradoEmUtc, SaidaApuracaoDevolucao Calculo)
{
    // Apenas projeta os valores persistidos; não consulta preços nem executa novamente o motor de cálculo.
    public static SaidaFinalDevolucao De(DemonstrativoFinal f) => new(f.Id, f.RevisaoId, f.CalculadoEmUtc,
        f.RegistradoEmUtc, new(f.VersaoRegra, f.SaidaUtc, f.RetornoPrevistoUtc, f.RetornoRealUtc,
            f.HodometroInicial, f.HodometroFinal, DecimalTransporte.Escrever(f.LitrosRestantes),
            DecimalTransporte.Escrever(f.CapacidadeLitros), DecimalTransporte.Escrever(f.CapacidadeLitros - f.LitrosRestantes),
            f.Diarias, f.Quilometros, f.FranquiaTotalKm, f.ExcedenteKm, f.RetornoRealUtc > f.RetornoPrevistoUtc,
            SaidaComponenteFinal.Bruto(f.TermoDiariasBruto), SaidaComponenteFinal.Bruto(f.TermoKmBruto),
            new(f.PrecoCombustivelId, f.TipoCombustivel.ToString(), f.InicioVigenciaCombustivelUtc,
                DecimalTransporte.Escrever(f.PrecoCombustivel)),
            f.Itens.OrderBy(i => i.TaxaServicoId).Select(i => new SaidaTaxaDevolucao(i.TaxaServicoId,
                VersaoCadastro.Etag(i.VersaoTaxa), i.Nome, i.Modalidade.ToString(),
                DecimalTransporte.Escrever(i.PrecoUnitario), i.OrigemInclusao.ToString())).ToArray(),
            f.Componentes.OrderBy(c => c.Ordem).Select(c => SaidaComponenteFinal.De(new(c.Categoria, c.OrigemId,
                c.TipoSeguro, c.Nome, c.Modalidade, c.OrigemInclusao, c.Quantidade, c.PrecoUnitario,
                c.ValorBruto, c.ValorArredondado))).ToArray(),
            SaidaComponenteFinal.Dinheiro(f.TotalFinal), SaidaComponenteFinal.Dinheiro(f.Caucao)));
}

public sealed record SaidaDevolucao(Guid AluguelId, string Estado, SaidaFinalDevolucao Devolucao);
