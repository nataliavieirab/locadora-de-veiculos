using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Pessoas;

public sealed record DadosCondutor(Guid ClienteId, string Nome, string Email, string Cpf,
    string Cnh, DateOnly ValidadeCnh, string Telefone);

public sealed class Condutor : CadastroExcluivel
{
    public Guid ClienteId { get; private set; }
    public string Nome { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string Cpf { get; private set; } = "";
    public string CpfNormalizado { get; private set; } = "";
    public string Cnh { get; private set; } = "";
    public string CnhNormalizada { get; private set; } = "";
    public DateOnly ValidadeCnh { get; private set; }
    public string Telefone { get; private set; } = "";

    private Condutor()
    {
    }

    private Condutor(Guid empresaId, DateTimeOffset agora) : base(empresaId, agora)
    {
    }

    public static Condutor Novo(Guid empresaId, DadosCondutor dados, DateTimeOffset agora)
    {
        var condutor = new Condutor(empresaId, agora);
        condutor.Alterar(dados, agora);
        return condutor;
    }

    public void Alterar(DadosCondutor dados, DateTimeOffset agora)
    {
        ExigirAtivo();
        ValidacaoCadastro.Referencia(dados.ClienteId, "clienteId");
        var nome = ValidacaoCadastro.Texto(dados.Nome, "nome", 200);
        var email = ValidacaoCadastro.Email(dados.Email, obrigatorio: true)!;
        var cpf = Documentos.Cpf(dados.Cpf);
        var cnh = ValidacaoCadastro.Texto(dados.Cnh, "cnh", 64);
        var normalizada = Documentos.Cnh(cnh);
        var telefone = ValidacaoCadastro.Texto(dados.Telefone, "telefone", 32);
        if (dados.ValidadeCnh == default)
        {
            throw new ValidacaoCadastroException("validadeCnh", "Informe a validade da CNH.");
        }

        RegistrarAlteracao(agora);
        ClienteId = dados.ClienteId;
        Nome = nome;
        Email = email;
        Cpf = dados.Cpf.Trim();
        CpfNormalizado = cpf;
        Cnh = cnh;
        CnhNormalizada = normalizada;
        ValidadeCnh = dados.ValidadeCnh;
        Telefone = telefone;
    }
}
