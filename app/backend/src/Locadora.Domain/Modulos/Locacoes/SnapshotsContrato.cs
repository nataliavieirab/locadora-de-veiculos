using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Pessoas;

namespace Locadora.Domain.Modulos.Locacoes;

public sealed class ClienteContratado
{
    public TipoCliente Tipo { get; private set; }
    public string Nome { get; private set; } = "";
    public string? Cpf { get; private set; }
    public string? Cnpj { get; private set; }
    public string? Rg { get; private set; }
    public string? Cnh { get; private set; }
    public string Telefone { get; private set; } = "";
    public string? Email { get; private set; }
    public Endereco Endereco { get; private set; } = null!;

    private ClienteContratado() { }

    internal static ClienteContratado Capturar(Cliente cliente) => new()
    {
        Tipo = cliente.Tipo, Nome = cliente.Nome, Cpf = cliente.Cpf, Cnpj = cliente.Cnpj,
        Rg = cliente.Rg, Cnh = cliente.Cnh, Telefone = cliente.Telefone, Email = cliente.Email,
        Endereco = new(cliente.Endereco.Estado, cliente.Endereco.Cidade, cliente.Endereco.Bairro,
            cliente.Endereco.Rua, cliente.Endereco.Numero),
    };
}

public sealed class CondutorContratado
{
    public string Nome { get; private set; } = "";
    public string Cpf { get; private set; } = "";
    public string Cnh { get; private set; } = "";
    public DateOnly ValidadeCnh { get; private set; }
    public string Email { get; private set; } = "";
    public string Telefone { get; private set; } = "";

    private CondutorContratado() { }

    internal static CondutorContratado Capturar(Condutor condutor) => new()
    {
        Nome = condutor.Nome, Cpf = condutor.Cpf, Cnh = condutor.Cnh,
        ValidadeCnh = condutor.ValidadeCnh, Email = condutor.Email, Telefone = condutor.Telefone,
    };
}

public sealed class VeiculoContratado
{
    public string Placa { get; private set; } = "";
    public string Marca { get; private set; } = "";
    public string Cor { get; private set; } = "";
    public string Modelo { get; private set; } = "";
    public TipoCombustivel Combustivel { get; private set; }
    public decimal CapacidadeLitros { get; private set; }
    public int Ano { get; private set; }
    public Guid GrupoId { get; private set; }
    public string GrupoNome { get; private set; } = "";

    private VeiculoContratado() { }

    internal static VeiculoContratado Capturar(Veiculo veiculo, Grupo grupo) => new()
    {
        Placa = veiculo.Placa, Marca = veiculo.Marca, Cor = veiculo.Cor, Modelo = veiculo.Modelo,
        Combustivel = veiculo.Combustivel, CapacidadeLitros = veiculo.CapacidadeLitros,
        Ano = veiculo.Ano, GrupoId = grupo.Id, GrupoNome = grupo.Nome,
    };
}

public sealed class PlanoContratado
{
    public ModalidadePlano Modalidade { get; private set; }
    public decimal PrecoDiario { get; private set; }
    public decimal? PrecoKm { get; private set; }
    public int? FranquiaDiariaKm { get; private set; }
    public decimal? PrecoKmExcedente { get; private set; }

    private PlanoContratado() { }

    internal static PlanoContratado Capturar(Plano plano) => new()
    {
        Modalidade = plano.Modalidade, PrecoDiario = plano.PrecoDiario, PrecoKm = plano.PrecoKm,
        FranquiaDiariaKm = plano.FranquiaDiariaKm, PrecoKmExcedente = plano.PrecoKmExcedente,
    };

    public ParametrosPlano Parametros() => new(Modalidade, PrecoDiario, PrecoKm, FranquiaDiariaKm, PrecoKmExcedente);
}
