using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Pessoas;

public enum TipoCliente { PF, PJ }

public sealed record DadosCliente(TipoCliente Tipo, string Nome, string? Cpf, string? Cnpj, string? Rg,
    string? Cnh, string Telefone, Endereco Endereco, string? Email);

public sealed class Cliente : CadastroExcluivel
{
    public TipoCliente Tipo { get; private set; }
    public string Nome { get; private set; } = "";
    public string? Cpf { get; private set; }
    public string? CpfNormalizado { get; private set; }
    public string? Cnpj { get; private set; }
    public string? CnpjNormalizado { get; private set; }
    public string? Rg { get; private set; }
    public string? Cnh { get; private set; }
    public string? CnhNormalizada { get; private set; }
    public string Telefone { get; private set; } = "";
    public string? Email { get; private set; }
    public Endereco Endereco { get; private set; } = null!;

    private Cliente()
    {
    }

    private Cliente(Guid empresaId, DateTimeOffset agora) : base(empresaId, agora)
    {
    }

    public static Cliente Novo(Guid empresaId, DadosCliente dados, DateTimeOffset agora)
    {
        var cliente = new Cliente(empresaId, agora);
        cliente.Alterar(dados, agora);
        return cliente;
    }

    public void Alterar(DadosCliente dados, DateTimeOffset agora)
    {
        ExigirAtivo();
        ValidacaoCadastro.Modalidade(dados.Tipo, "tipo");
        var nome = ValidacaoCadastro.Texto(dados.Nome, "nome", 200);
        var telefone = ValidacaoCadastro.Texto(dados.Telefone, "telefone", 32);
        var email = ValidacaoCadastro.Email(dados.Email);
        if (dados.Endereco is null)
        {
            throw new ValidacaoCadastroException("endereco", "Informe o endereço completo.");
        }

        string? cpf = null, cnpj = null, rg = null, cnh = null, cnhNormalizada = null;
        if (dados.Tipo == TipoCliente.PF)
        {
            cpf = Documentos.Cpf(dados.Cpf);
            rg = ValidacaoCadastro.Texto(dados.Rg, "rg", 64);
            cnh = ValidacaoCadastro.Texto(dados.Cnh, "cnh", 64);
            cnhNormalizada = Documentos.Cnh(cnh);
            if (!string.IsNullOrWhiteSpace(dados.Cnpj))
            {
                throw new ValidacaoCadastroException("cnpj", "Cliente PF não possui campo CNPJ.");
            }
        }
        else
        {
            cnpj = Documentos.Cnpj(dados.Cnpj);
            if (!string.IsNullOrWhiteSpace(dados.Cpf) || !string.IsNullOrWhiteSpace(dados.Rg) || !string.IsNullOrWhiteSpace(dados.Cnh))
            {
                throw new ValidacaoCadastroException("tipo", "Cliente PJ não possui os documentos exclusivos de PF.");
            }
        }

        RegistrarAlteracao(agora);
        Tipo = dados.Tipo;
        Nome = nome;
        Cpf = cpf is null ? null : dados.Cpf!.Trim();
        CpfNormalizado = cpf;
        Cnpj = cnpj is null ? null : dados.Cnpj!.Trim();
        CnpjNormalizado = cnpj;
        Rg = rg;
        Cnh = cnh;
        CnhNormalizada = cnhNormalizada;
        Telefone = telefone;
        Email = email;
        Endereco = dados.Endereco;
    }
}
