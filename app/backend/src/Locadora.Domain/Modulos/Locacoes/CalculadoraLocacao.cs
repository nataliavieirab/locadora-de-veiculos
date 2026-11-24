using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public enum CategoriaItem { TaxaServico, Seguro }

public sealed record ParametrosPlano(ModalidadePlano Modalidade, decimal PrecoDiario,
    decimal? PrecoKm, int? FranquiaDiariaKm, decimal? PrecoKmExcedente)
{
    public static ParametrosPlano Capturar(Plano plano) => new(plano.Modalidade, plano.PrecoDiario,
        plano.PrecoKm, plano.FranquiaDiariaKm, plano.PrecoKmExcedente);

    public void Validar()
    {
        ValidacaoCadastro.Modalidade(Modalidade, "modalidade");
        ValidacaoCadastro.Preco(PrecoDiario, "precoDiario");
        if (PrecoKm is not null) ValidacaoCadastro.Preco(PrecoKm.Value, "precoKm");
        if (PrecoKmExcedente is not null) ValidacaoCadastro.Preco(PrecoKmExcedente.Value, "precoKmExcedente");
        var valido = Modalidade switch
        {
            ModalidadePlano.Diario => PrecoKm is not null && FranquiaDiariaKm is null && PrecoKmExcedente is null,
            ModalidadePlano.Controlado => PrecoKm is null && FranquiaDiariaKm is >= 0 && PrecoKmExcedente is not null,
            ModalidadePlano.Livre => PrecoKm is null && FranquiaDiariaKm is null && PrecoKmExcedente is null,
            _ => false,
        };
        if (!valido)
        {
            throw new ValidacaoCadastroException("planoId", "Os parâmetros não correspondem à modalidade do plano.");
        }
    }
}

public sealed record AdicionalCalculo(CategoriaItem Categoria, Guid OrigemId, TipoSeguro? TipoSeguro,
    string Nome, ModalidadeTaxa Modalidade, decimal PrecoUnitario);

public sealed record ComponenteCalculado(CategoriaItem? Categoria, Guid OrigemId, TipoSeguro? TipoSeguro,
    string Nome, decimal PrecoUnitario, int Multiplicador, decimal ValorBruto, decimal ValorArredondado);

public sealed record ResultadoCalculo(int Diarias, int? Quilometros, long? FranquiaTotalKm,
    bool QuilometragemPendente, decimal TermoDiariasBruto, decimal TermoKmBruto,
    IReadOnlyList<ComponenteCalculado> Componentes, decimal TotalConhecido)
{
    public const decimal Caucao = 1000.00m;
    public const string VersaoRegra = "contratacao-v1";
    public bool CombustivelPendente => true;
    public bool AtrasoPendente => true;
}

public static class CalculadoraLocacao
{
    public const decimal MaximoMonetario = 99999999999999999.99m;

    public static int QuilometrosRodados(int inicial, int final)
    {
        if (inicial < 0 || final < inicial)
        {
            throw new ValidacaoCadastroException("hodometroFinal", "O hodômetro final deve ser inteiro e não inferior ao inicial.");
        }
        return final - inicial;
    }

    // K é nullable: estimar não inventa quilometragem zero nem produz fechamento.
    public static ResultadoCalculo Calcular(DateTimeOffset saida, DateTimeOffset retorno,
        Guid planoId, ParametrosPlano plano, IReadOnlyCollection<AdicionalCalculo> adicionais, int? quilometros = null)
    {
        ValidacaoCadastro.Referencia(planoId, "planoId");
        plano.Validar();
        if (quilometros < 0)
        {
            throw new ValidacaoCadastroException("quilometros", "Informe quilômetros inteiros não negativos.");
        }
        var identidades = new HashSet<(CategoriaItem Categoria, string Origem)>();
        foreach (var adicional in adicionais)
        {
            ValidacaoCadastro.Modalidade(adicional.Categoria, "itens");
            ValidacaoCadastro.Modalidade(adicional.Modalidade, "itens");
            ValidacaoCadastro.Referencia(adicional.OrigemId, "itens");
            ValidacaoCadastro.Texto(adicional.Nome, "itens", 200);
            ValidacaoCadastro.Preco(adicional.PrecoUnitario, "itens");
            if (adicional.Categoria == CategoriaItem.Seguro)
            {
                if (adicional.TipoSeguro is null || adicional.Modalidade != ModalidadeTaxa.Diaria)
                    throw new ValidacaoCadastroException("seguros", "Informe um seguro diário configurado.");
                ValidacaoCadastro.Modalidade(adicional.TipoSeguro.Value, "seguros");
            }
            else if (adicional.TipoSeguro is not null)
            {
                throw new ValidacaoCadastroException("taxasServicoIds", "Uma taxa não possui tipo de seguro.");
            }
            var origem = adicional.Categoria == CategoriaItem.Seguro
                ? adicional.TipoSeguro!.Value.ToString() : adicional.OrigemId.ToString("N");
            if (!identidades.Add((adicional.Categoria, origem)))
                throw new ValidacaoCadastroException("itens", "Não repita a mesma taxa ou seguro.");
        }

        try
        {
            var diarias = TempoLocacao.ContarDiarias(saida, retorno);
            long? franquia = plano.Modalidade == ModalidadePlano.Controlado
                ? checked((long)diarias * plano.FranquiaDiariaKm!.Value) : null;
            var termoDiarias = checked(diarias * plano.PrecoDiario);
            var termoKm = quilometros is null ? 0m : plano.Modalidade switch
            {
                ModalidadePlano.Diario => checked(quilometros.Value * plano.PrecoKm!.Value),
                ModalidadePlano.Controlado => checked(Math.Max(0L, quilometros.Value - franquia!.Value) * plano.PrecoKmExcedente!.Value),
                _ => 0m,
            };
            var brutoPlano = checked(termoDiarias + termoKm);
            var componentes = new List<ComponenteCalculado>
            {
                new(null, planoId, null, $"Plano {plano.Modalidade}", plano.PrecoDiario,
                    diarias, brutoPlano, ArredondarComponente(brutoPlano)),
            };
            foreach (var adicional in adicionais)
            {
                var multiplicador = adicional.Modalidade == ModalidadeTaxa.Fixa ? 1 : diarias;
                var bruto = checked(adicional.PrecoUnitario * multiplicador);
                componentes.Add(new(adicional.Categoria, adicional.OrigemId, adicional.TipoSeguro, adicional.Nome,
                    adicional.PrecoUnitario, multiplicador, bruto, ArredondarComponente(bruto)));
            }
            var total = componentes.Sum(componente => componente.ValorArredondado);
            ExigirIntervalo(total);
            return new(diarias, quilometros, franquia,
                quilometros is null && plano.Modalidade != ModalidadePlano.Livre,
                termoDiarias, termoKm, componentes.AsReadOnly(), total);
        }
        catch (OverflowException)
        {
            throw new ValidacaoCadastroException("estimativa", "Os valores excedem o intervalo monetário permitido.");
        }
    }

    public static decimal ArredondarComponente(decimal bruto)
    {
        if (bruto < 0 || ValidacaoCadastro.Escala(bruto) > 9)
            throw new ValidacaoCadastroException("estimativa", "Componente inválido ou com precisão excessiva.");
        var arredondado = decimal.Round(bruto, 2, MidpointRounding.AwayFromZero);
        ExigirIntervalo(arredondado);
        return arredondado;
    }

    private static void ExigirIntervalo(decimal valor)
    {
        if (valor < 0 || valor > MaximoMonetario)
            throw new ValidacaoCadastroException("estimativa", "Os valores excedem o intervalo monetário permitido.");
    }
}
