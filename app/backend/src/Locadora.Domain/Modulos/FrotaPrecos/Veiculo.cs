using Locadora.Domain.Modulos.Transversal;
using Locadora.Domain.Modulos.Fotos;

namespace Locadora.Domain.Modulos.FrotaPrecos;

public enum TipoCombustivel { Gasolina, Gas, Diesel, Alcool }

public sealed record DadosVeiculo(string Placa, string Marca, string Cor, string Modelo,
    TipoCombustivel Combustivel, decimal CapacidadeLitros, int Ano, Guid GrupoId);

public sealed class Veiculo : CadastroExcluivel
{
    public string Placa { get; private set; } = "";
    public string PlacaNormalizada { get; private set; } = "";
    public string Marca { get; private set; } = "";
    public string Cor { get; private set; } = "";
    public string Modelo { get; private set; } = "";
    public TipoCombustivel Combustivel { get; private set; }
    public decimal CapacidadeLitros { get; private set; }
    public int Ano { get; private set; }
    public Guid GrupoId { get; private set; }
    public Guid? FotoAtivaId { get; private set; }

    private Veiculo()
    {
    }

    private Veiculo(Guid empresaId, DateTimeOffset agora) : base(empresaId, agora)
    {
    }

    public static Veiculo Novo(Guid empresaId, DadosVeiculo dados, DateTimeOffset agora)
    {
        var veiculo = new Veiculo(empresaId, agora);
        veiculo.Alterar(dados, agora);
        return veiculo;
    }

    public void DefinirFoto(FotoObjeto nova, FotoObjeto? anterior, DateTimeOffset agora)
    {
        ExigirAtivo();
        ExigirFoto(anterior, agora);
        if (nova.EmpresaId != EmpresaId || nova.VeiculoId != Id || nova.Estado != EstadoFoto.Pendente
            || agora < nova.AlteradoEmUtc)
            throw new InvalidOperationException("A nova foto não pertence à pendência do veículo.");
        RegistrarAlteracao(agora);
        anterior?.Inativar(agora);
        nova.Ativar(agora);
        FotoAtivaId = nova.Id;
    }

    public void RemoverFoto(FotoObjeto? atual, DateTimeOffset agora)
    {
        ExigirAtivo();
        ExigirFoto(atual, agora);
        if (atual is null) return;
        RegistrarAlteracao(agora);
        atual.Inativar(agora);
        FotoAtivaId = null;
    }

    private void ExigirFoto(FotoObjeto? atual, DateTimeOffset agora)
    {
        if (agora < AlteradoEmUtc || atual?.Id != FotoAtivaId
            || atual is not null && (atual.EmpresaId != EmpresaId || atual.VeiculoId != Id
                || atual.Estado != EstadoFoto.Ativa || agora < atual.AlteradoEmUtc))
            throw new InvalidOperationException("A referência atual da foto não é válida.");
    }

    public void Alterar(DadosVeiculo dados, DateTimeOffset agora)
    {
        ExigirAtivo();
        var placa = ValidacaoCadastro.Texto(dados.Placa, "placa", 16);
        var normalizada = string.Concat(placa.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
        ValidacaoCadastro.Texto(normalizada, "placa", 16);
        var marca = ValidacaoCadastro.Texto(dados.Marca, "marca", 100);
        var cor = ValidacaoCadastro.Texto(dados.Cor, "cor", 100);
        var modelo = ValidacaoCadastro.Texto(dados.Modelo, "modelo", 100);
        ValidacaoCadastro.Modalidade(dados.Combustivel, "combustivel");
        ValidacaoCadastro.Referencia(dados.GrupoId, "grupoId");
        if (dados.CapacidadeLitros <= 0 || dados.CapacidadeLitros > 999999999.999m
            || ValidacaoCadastro.Escala(dados.CapacidadeLitros) > 3)
        {
            throw new ValidacaoCadastroException("capacidadeLitros", "Informe uma capacidade positiva com até três casas decimais e nove dígitos inteiros.");
        }
        if (dados.Ano <= 0)
        {
            throw new ValidacaoCadastroException("ano", "Informe um ano positivo.");
        }

        RegistrarAlteracao(agora);
        Placa = placa;
        PlacaNormalizada = normalizada;
        Marca = marca;
        Cor = cor;
        Modelo = modelo;
        Combustivel = dados.Combustivel;
        CapacidadeLitros = dados.CapacidadeLitros;
        Ano = dados.Ano;
        GrupoId = dados.GrupoId;
    }
}
