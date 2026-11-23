using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Pessoas;

public sealed class Endereco
{
    public string Estado { get; private set; } = "";
    public string Cidade { get; private set; } = "";
    public string Bairro { get; private set; } = "";
    public string Rua { get; private set; } = "";
    public string Numero { get; private set; } = "";

    private Endereco()
    {
    }

    public Endereco(string estado, string cidade, string bairro, string rua, string numero)
    {
        Estado = ValidacaoCadastro.Texto(estado, "endereco.estado", 100);
        Cidade = ValidacaoCadastro.Texto(cidade, "endereco.cidade", 100);
        Bairro = ValidacaoCadastro.Texto(bairro, "endereco.bairro", 100);
        Rua = ValidacaoCadastro.Texto(rua, "endereco.rua", 200);
        Numero = ValidacaoCadastro.Texto(numero, "endereco.numero", 200);
    }
}
