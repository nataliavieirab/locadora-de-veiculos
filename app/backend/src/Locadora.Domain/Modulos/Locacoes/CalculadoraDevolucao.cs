using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public enum CategoriaComponenteFinal { Plano, TaxaServico, Seguro, Combustivel, Multa }

public sealed record CombustivelCalculo(Guid Id, TipoCombustivel Tipo, DateTimeOffset InicioVigenciaUtc,
    decimal PrecoUnitario);

public sealed record AdicionalCalculoFinal(AdicionalCalculo Item, OrigemInclusao OrigemInclusao);

public sealed record EntradaCalculoFinal(DateTimeOffset Saida, DateTimeOffset RetornoPrevisto,
    DateTimeOffset RetornoReal, int HodometroInicial, int HodometroFinal,
    Guid PlanoId, ParametrosPlano Plano, TipoCombustivel TipoCombustivel,
    decimal CapacidadeLitros, decimal LitrosRestantes, CombustivelCalculo? Combustivel,
    IReadOnlyCollection<AdicionalCalculoFinal> Adicionais);

public sealed record ComponenteFinalCalculado(CategoriaComponenteFinal Categoria, Guid? OrigemId,
    TipoSeguro? TipoSeguro, string Nome, ModalidadeTaxa? Modalidade, OrigemInclusao? OrigemInclusao,
    decimal Quantidade, decimal PrecoUnitario, decimal ValorBruto, decimal ValorArredondado);

public sealed record ResultadoCalculoFinal(EntradaCalculoFinal Entradas, int Diarias, int Quilometros,
    long? FranquiaTotalKm, long? ExcedenteKm, decimal TermoDiariasBruto, decimal TermoKmBruto,
    decimal LitrosRepostos, bool EmAtraso, IReadOnlyList<ComponenteFinalCalculado> Componentes,
    decimal TotalFinal)
{
    public const decimal Caucao = 1000.00m;
    public const string VersaoRegra = "devolucao-v1";
}

public static class CalculadoraDevolucao
{
    public static ResultadoCalculoFinal Calcular(EntradaCalculoFinal entrada)
    {
        if (entrada.RetornoPrevisto <= entrada.Saida)
            throw new ValidacaoCadastroException("retornoPrevisto", "O retorno previsto deve ser posterior à saída.");
        if (entrada.RetornoReal < entrada.Saida)
            throw new ValidacaoCadastroException("retornoReal", "O retorno real não pode preceder a saída.");
        if (entrada.HodometroInicial < 0)
            throw new ValidacaoCadastroException("hodometroInicial", "Informe um hodômetro inteiro não negativo.");
        var quilometros = CalculadoraLocacao.QuilometrosRodados(entrada.HodometroInicial, entrada.HodometroFinal);
        ValidarLitros(entrada.CapacidadeLitros, entrada.LitrosRestantes);
        ValidacaoCadastro.Modalidade(entrada.TipoCombustivel, "combustivel");
        var combustivel = entrada.Combustivel;
        // Mesmo tanque cheio exige uma vigência explícita. A seleção da última versão pertence à aplicação.
        if (combustivel is null || combustivel.InicioVigenciaUtc > entrada.RetornoReal)
            throw new ValidacaoCadastroException("combustivel", "Informe um preço de combustível vigente no retorno real.");
        ValidacaoCadastro.Referencia(combustivel.Id, "combustivel");
        ValidacaoCadastro.Modalidade(combustivel.Tipo, "combustivel");
        if (combustivel.Tipo != entrada.TipoCombustivel)
            throw new ValidacaoCadastroException("combustivel", "O preço deve corresponder ao combustível contratado.");
        ValidacaoCadastro.Preco(combustivel.PrecoUnitario, "combustivel");

        // Copiar a coleção impede que uma alteração posterior da entrada reescreva o resultado.
        var adicionais = entrada.Adicionais.ToArray();
        foreach (var adicional in adicionais)
        {
            ValidacaoCadastro.Modalidade(adicional.OrigemInclusao, "itens");
            if (adicional.OrigemInclusao == OrigemInclusao.Devolucao && adicional.Item.Categoria != CategoriaItem.TaxaServico)
                throw new ValidacaoCadastroException("itens", "Somente taxas podem ser incluídas na devolução.");
        }
        ResultadoCalculo baseCalculo;
        try
        {
            baseCalculo = CalculadoraLocacao.Calcular(entrada.Saida, entrada.RetornoReal, entrada.PlanoId,
                entrada.Plano, adicionais.Select(adicional => adicional.Item).ToArray(), quilometros);
        }
        catch (ValidacaoCadastroException excecao) when (excecao.Campo == "estimativa")
        {
            throw new ValidacaoCadastroException("devolucao", excecao.Message);
        }
        var componentes = new List<ComponenteFinalCalculado>();
        for (var indice = 0; indice < baseCalculo.Componentes.Count; indice++)
        {
            var componente = baseCalculo.Componentes[indice];
            var adicional = indice == 0 ? null : adicionais[indice - 1];
            var categoria = componente.Categoria switch
            {
                CategoriaItem.TaxaServico => CategoriaComponenteFinal.TaxaServico,
                CategoriaItem.Seguro => CategoriaComponenteFinal.Seguro,
                _ => CategoriaComponenteFinal.Plano,
            };
            componentes.Add(new(categoria, componente.OrigemId, componente.TipoSeguro, componente.Nome,
                adicional?.Item.Modalidade, adicional?.OrigemInclusao, componente.Multiplicador,
                componente.PrecoUnitario, componente.ValorBruto, componente.ValorArredondado));
        }

        try
        {
            var litrosRepostos = entrada.CapacidadeLitros - entrada.LitrosRestantes;
            var brutoCombustivel = checked(litrosRepostos * combustivel.PrecoUnitario);
            componentes.Add(new(CategoriaComponenteFinal.Combustivel, combustivel.Id, null,
                $"Reposição de {entrada.TipoCombustivel}", null, null, litrosRepostos, combustivel.PrecoUnitario,
                brutoCombustivel, Arredondar(brutoCombustivel)));
            var emAtraso = entrada.RetornoReal > entrada.RetornoPrevisto;
            // RN-08/09: o plano é um único componente; a multa usa B já arredondado, nunca seus termos brutos.
            var brutoMulta = emAtraso ? checked(componentes[0].ValorArredondado * 0.10m) : 0m;
            componentes.Add(new(CategoriaComponenteFinal.Multa, null, null,
                emAtraso ? "Multa por atraso" : "Multa · sem atraso", null, null,
                emAtraso ? 1m : 0m, 0.10m, brutoMulta, Arredondar(brutoMulta)));
            var total = componentes.Sum(componente => componente.ValorArredondado);
            if (total > CalculadoraLocacao.MaximoMonetario)
                throw new ValidacaoCadastroException("devolucao", "Os valores excedem o intervalo monetário permitido.");
            var entradasPreservadas = entrada with
            {
                Saida = entrada.Saida.ToUniversalTime(), RetornoPrevisto = entrada.RetornoPrevisto.ToUniversalTime(),
                RetornoReal = entrada.RetornoReal.ToUniversalTime(),
                Combustivel = combustivel with { InicioVigenciaUtc = combustivel.InicioVigenciaUtc.ToUniversalTime() },
                Adicionais = Array.AsReadOnly(adicionais),
            };
            return new(entradasPreservadas, baseCalculo.Diarias, quilometros, baseCalculo.FranquiaTotalKm,
                baseCalculo.FranquiaTotalKm is { } franquia ? Math.Max(0L, quilometros - franquia) : null,
                baseCalculo.TermoDiariasBruto, baseCalculo.TermoKmBruto, litrosRepostos, emAtraso,
                componentes.AsReadOnly(), total);
        }
        catch (OverflowException)
        {
            throw new ValidacaoCadastroException("devolucao", "Os valores excedem o intervalo monetário permitido.");
        }
    }

    private static decimal Arredondar(decimal bruto)
    {
        try
        {
            return CalculadoraLocacao.ArredondarComponente(bruto);
        }
        catch (ValidacaoCadastroException excecao)
        {
            throw new ValidacaoCadastroException("devolucao", excecao.Message);
        }
    }

    private static void ValidarLitros(decimal capacidade, decimal restantes)
    {
        if (capacidade <= 0 || capacidade > 999999999.999m || ValidacaoCadastro.Escala(capacidade) > 3)
            throw new ValidacaoCadastroException("capacidadeLitros", "Informe capacidade positiva com até três casas decimais e nove dígitos inteiros.");
        if (restantes < 0 || restantes > capacidade || ValidacaoCadastro.Escala(restantes) > 3)
            throw new ValidacaoCadastroException("litrosRestantes", "Informe litros entre zero e a capacidade, com até três casas decimais.");
    }
}
