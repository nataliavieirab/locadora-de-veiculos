using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public static class AceiteDevolucao
{
    public static VersoesDevolucaoAceitas Criar(Guid empresaId, Guid contaId, Aluguel aluguel,
        DadosDevolucao dados, ResultadoCalculoFinal calculo)
    {
        ValidacaoCadastro.Referencia(empresaId, "empresaId");
        ValidacaoCadastro.Referencia(contaId, "contaId");
        var taxas = dados.TaxasAdicionais.OrderBy(t => t.Id).Select(t =>
            new VersaoTaxaAceita(t.Id, VersaoCadastro.Etag(t.RowVersion))).ToArray();
        var entrada = calculo.Entradas;
        var canonico = new
        {
            Empresa = empresaId, Ator = contaId, Aluguel = aluguel.Id, Revisao = aluguel.RevisaoAtual.Id,
            Versao = VersaoCadastro.Etag(aluguel.RowVersion), Regra = ResultadoCalculoFinal.VersaoRegra,
            Saida = entrada.Saida.UtcTicks, Previsao = entrada.RetornoPrevisto.UtcTicks, Retorno = entrada.RetornoReal.UtcTicks,
            entrada.HodometroInicial, entrada.HodometroFinal, entrada.PlanoId,
            Modalidade = entrada.Plano.Modalidade.ToString(), Diaria = Decimal(entrada.Plano.PrecoDiario),
            Km = entrada.Plano.PrecoKm is { } km ? Decimal(km) : null, entrada.Plano.FranquiaDiariaKm,
            Excedente = entrada.Plano.PrecoKmExcedente is { } excedente ? Decimal(excedente) : null,
            Combustivel = entrada.Combustivel!.Id, Vigencia = entrada.Combustivel.InicioVigenciaUtc.UtcTicks,
            TipoCombustivel = entrada.TipoCombustivel.ToString(), PrecoCombustivel = Decimal(entrada.Combustivel.PrecoUnitario),
            Capacidade = Decimal(entrada.CapacidadeLitros), Restantes = Decimal(entrada.LitrosRestantes),
            Taxas = taxas, calculo.Diarias, calculo.Quilometros, calculo.FranquiaTotalKm, calculo.ExcedenteKm,
            DiariasBruto = Decimal(calculo.TermoDiariasBruto), KmBruto = Decimal(calculo.TermoKmBruto),
            Componentes = calculo.Componentes.OrderBy(c => c.Categoria).ThenBy(c => c.OrigemId).Select(c => new
            {
                Categoria = c.Categoria.ToString(), c.OrigemId, c.TipoSeguro, c.Nome, c.Modalidade, c.OrigemInclusao,
                Quantidade = Decimal(c.Quantidade), Preco = Decimal(c.PrecoUnitario),
                Bruto = Decimal(c.ValorBruto), Arredondado = Decimal(c.ValorArredondado),
            }).ToArray(),
            Total = Decimal(calculo.TotalFinal), Caucao = Decimal(ResultadoCalculoFinal.Caucao),
        };
        var resumo = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonico)));
        return new(aluguel.RevisaoAtual.Id, ResultadoCalculoFinal.VersaoRegra, entrada.Combustivel.Id,
            Array.AsReadOnly(taxas), resumo);
    }

    public static void Validar(VersoesDevolucaoAceitas aceite)
    {
        if (aceite is null || aceite.RevisaoId == Guid.Empty || aceite.CombustivelId == Guid.Empty
            || string.IsNullOrWhiteSpace(aceite.VersaoRegra) || aceite.VersaoRegra.Length > 64
            || aceite.TaxasNovas is null || aceite.TaxasNovas.Any(t => t is null)
            || aceite.TaxasNovas.Select(t => t.Id).Distinct().Count() != aceite.TaxasNovas.Count
            || aceite.TaxasNovas.Any(t => t.Id == Guid.Empty || string.IsNullOrWhiteSpace(t.Versao) || t.Versao.Length > 64)
            || aceite.ResumoCalculo is null || aceite.ResumoCalculo.Length != 64 || aceite.ResumoCalculo.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ValidacaoCadastroException("versoesAceitas", "Informe as versões e o resumo da prévia aceita.");
    }

    public static void Exigir(VersoesDevolucaoAceitas aceito, VersoesDevolucaoAceitas atual)
    {
        Validar(aceito);
        if (aceito.RevisaoId != atual.RevisaoId || aceito.VersaoRegra != atual.VersaoRegra
            || aceito.CombustivelId != atual.CombustivelId || aceito.ResumoCalculo != atual.ResumoCalculo
            || !aceito.TaxasNovas.OrderBy(t => t.Id).SequenceEqual(atual.TaxasNovas.OrderBy(t => t.Id)))
            throw new ConflitoCadastroException("previa_desatualizada");
    }

    private static string Decimal(decimal valor) => valor.ToString("G29", CultureInfo.InvariantCulture);
}
