using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public enum OrigemInclusao { Contratacao, Edicao }

public sealed class ItemContratado : IEntidadeEmpresa
{
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid RevisaoId { get; private set; }
    public CategoriaItem Categoria { get; private set; }
    public Guid OrigemId { get; private set; }
    public Guid? TaxaServicoId { get; private set; }
    public Guid? PrecoSeguroId { get; private set; }
    public TipoSeguro? TipoSeguro { get; private set; }
    public string Nome { get; private set; } = "";
    public ModalidadeTaxa Modalidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public OrigemInclusao OrigemInclusao { get; private set; }
    public int RevisaoPrimeiraInclusao { get; private set; }

    private ItemContratado() { }

    internal static ItemContratado Capturar(Guid empresaId, Guid revisaoId, int numero,
        AdicionalCalculo atual, ItemContratado? mantido) => new()
    {
        Id = Guid.NewGuid(), EmpresaId = empresaId, RevisaoId = revisaoId,
        Categoria = atual.Categoria, OrigemId = atual.OrigemId,
        TaxaServicoId = atual.Categoria == CategoriaItem.TaxaServico ? atual.OrigemId : null,
        PrecoSeguroId = atual.Categoria == CategoriaItem.Seguro ? atual.OrigemId : null,
        TipoSeguro = atual.TipoSeguro, Nome = mantido?.Nome ?? atual.Nome,
        Modalidade = mantido?.Modalidade ?? atual.Modalidade,
        PrecoUnitario = mantido?.PrecoUnitario ?? atual.PrecoUnitario,
        OrigemInclusao = mantido?.OrigemInclusao ?? (numero == 1 ? OrigemInclusao.Contratacao : OrigemInclusao.Edicao),
        RevisaoPrimeiraInclusao = mantido?.RevisaoPrimeiraInclusao ?? numero,
    };

    public AdicionalCalculo ParaCalculo() => new(Categoria, OrigemId, TipoSeguro, Nome, Modalidade, PrecoUnitario);
}
