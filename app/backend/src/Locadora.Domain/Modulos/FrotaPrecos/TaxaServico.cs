using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.FrotaPrecos;

public enum ModalidadeTaxa { Fixa, Diaria }

public sealed class TaxaServico : CadastroExcluivel
{
    public string Nome { get; private set; } = "";
    public decimal Preco { get; private set; }
    public ModalidadeTaxa Modalidade { get; private set; }

    private TaxaServico()
    {
    }

    private TaxaServico(Guid empresaId, DateTimeOffset agora) : base(empresaId, agora)
    {
    }

    public static TaxaServico Nova(Guid empresaId, string nome, decimal preco, ModalidadeTaxa modalidade, DateTimeOffset agora)
    {
        var taxa = new TaxaServico(empresaId, agora);
        taxa.Alterar(nome, preco, modalidade, agora);
        return taxa;
    }

    public void Alterar(string nome, decimal preco, ModalidadeTaxa modalidade, DateTimeOffset agora)
    {
        ExigirAtivo();
        var validado = ValidacaoCadastro.Texto(nome, "nome", 200);
        ValidacaoCadastro.Preco(preco, "preco");
        ValidacaoCadastro.Modalidade(modalidade, "modalidade");
        RegistrarAlteracao(agora);
        Nome = validado;
        Preco = preco;
        Modalidade = modalidade;
    }
}
