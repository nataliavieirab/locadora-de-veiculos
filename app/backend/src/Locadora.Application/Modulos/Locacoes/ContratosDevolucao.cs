using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public sealed record ComandoDevolucao(DateTimeOffset RetornoReal, int HodometroFinal,
    decimal LitrosRestantes, IReadOnlyList<Guid> TaxasAdicionaisIds)
{
    public void Validar()
    {
        if (HodometroFinal < 0)
            throw new ValidacaoCadastroException("hodometroFinal", "Informe um hodômetro inteiro não negativo.");
        if (LitrosRestantes < 0 || LitrosRestantes > 999999999.999m || ValidacaoCadastro.Escala(LitrosRestantes) > 3)
            throw new ValidacaoCadastroException("litrosRestantes", "Informe litros não negativos com até três casas decimais e nove dígitos inteiros.");
        if (TaxasAdicionaisIds is null)
            throw new ValidacaoCadastroException("taxasAdicionaisIds", "Informe a lista de taxas, mesmo quando vazia.");
        if (TaxasAdicionaisIds.Count != TaxasAdicionaisIds.Distinct().Count())
            throw new ValidacaoCadastroException("taxasAdicionaisIds", "Não repita taxas adicionais.");
        foreach (var id in TaxasAdicionaisIds) ValidacaoCadastro.Referencia(id, "taxasAdicionaisIds");
    }
}

public sealed record VersaoTaxaAceita(Guid Id, string Versao);
public sealed record VersoesDevolucaoAceitas(Guid RevisaoId, string VersaoRegra, Guid CombustivelId,
    IReadOnlyList<VersaoTaxaAceita> TaxasNovas, string ResumoCalculo);
public sealed record ComandoConclusaoDevolucao(ComandoDevolucao Dados, VersoesDevolucaoAceitas VersoesAceitas);
public sealed record ResultadoPreviaDevolucao(Guid AluguelId, Guid RevisaoId, int Revisao, byte[] RowVersion,
    ResultadoCalculoFinal Calculo, VersoesDevolucaoAceitas VersoesAceitas);
public sealed record ResultadoDevolucao(Guid AluguelId, DemonstrativoFinal Demonstrativo, byte[] RowVersion);
public sealed record ResultadoConsultaAluguel(Guid AluguelId, RevisaoContrato Revisao, EstadoAluguel Estado,
    bool Oculto, DemonstrativoFinal? Devolucao, byte[] RowVersion);
public sealed record ReferenciaVeiculoHistorico(Guid Id, string Placa, string Modelo);

// Empresa e ator vêm de IContextoLocacao, nunca destes comandos ou de parâmetros de consulta.
public interface IConsultasDevolucao
{
    Task<Aluguel?> AluguelAsync(Guid id, CancellationToken cancelamento);
    Task<DadosDevolucao> CarregarAsync(Aluguel aluguel, ComandoDevolucao comando, CancellationToken cancelamento,
        bool confirmarAceite = false);
    Task<byte[]?> VersaoAluguelAsync(Guid id, CancellationToken cancelamento);
}

public interface IRepositorioFechamentoAluguel : IRepositorioAlugueis
{
    Task<Aluguel?> ConsultarIncluindoOcultoAsync(Guid id, CancellationToken cancelamento);
    Task<ComandoIdempotente?> ComandoAsync(Guid contaId, string operacao, Guid chave, CancellationToken cancelamento);
    Task<DemonstrativoFinal?> FinalAsync(Guid id, CancellationToken cancelamento);
}

public interface IConsultasHistoricoAluguel
{
    Task<ResultadoConsultaAluguel> ConsultarAsync(Guid id, bool incluirOcultos, CancellationToken cancelamento);
    Task<Pagina<ReferenciaVeiculoHistorico>> ReferenciasVeiculosAsync(Paginacao paginacao,
        bool incluirOcultos, CancellationToken cancelamento);
}

public interface IOperacoesDevolucao
{
    Task<ResultadoPreviaDevolucao> PreviaAsync(Guid id, ComandoDevolucao comando, string? versao,
        CancellationToken cancelamento);
    Task<ResultadoDevolucao> ConcluirAsync(Guid id, ComandoConclusaoDevolucao comando, string? versao,
        Guid chave, CancellationToken cancelamento);
}

public interface IOcultacaoAluguel
{
    Task OcultarAsync(Guid id, string? versao, CancellationToken cancelamento);
}
