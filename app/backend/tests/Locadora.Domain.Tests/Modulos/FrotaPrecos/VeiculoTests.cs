using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.FrotaPrecos;

[TestClass]
public sealed class VeiculoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static DadosVeiculo Dados => new("abc-1D23", "Marca sintética", "Azul", "Modelo sintético", TipoCombustivel.Gasolina, 48.125m, 2026, Guid.NewGuid());

    [TestMethod]
    public void CriaSemFotoNormalizaPlacaEPreservaCampos()
    {
        var dados = Dados;
        var veiculo = Veiculo.Novo(Guid.NewGuid(), dados, Agora);
        Assert.AreEqual(dados.Placa, veiculo.Placa);
        Assert.AreEqual("ABC1D23", veiculo.PlacaNormalizada);
        Assert.AreEqual(dados.Marca, veiculo.Marca);
        Assert.AreEqual(dados.Cor, veiculo.Cor);
        Assert.AreEqual(dados.Modelo, veiculo.Modelo);
        Assert.AreEqual(dados.Combustivel, veiculo.Combustivel);
        Assert.AreEqual(dados.CapacidadeLitros, veiculo.CapacidadeLitros);
        Assert.AreEqual(dados.Ano, veiculo.Ano);
        Assert.AreEqual(dados.GrupoId, veiculo.GrupoId);
        Assert.IsFalse(typeof(Veiculo).GetProperties().Any(propriedade => propriedade.Name.Contains("Foto", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void EdicaoValidaPreservaIdentidadeEExclusaoNaoRestauraRegistro()
    {
        var dados = Dados;
        var veiculo = Veiculo.Novo(Guid.NewGuid(), dados, Agora);
        var id = veiculo.Id;
        veiculo.Alterar(dados with { Placa = "XYZ-9A87", Cor = "Prata", Combustivel = TipoCombustivel.Alcool }, Agora.AddMinutes(1));
        Assert.AreEqual(id, veiculo.Id);
        Assert.AreEqual("XYZ9A87", veiculo.PlacaNormalizada);
        Assert.AreEqual("Prata", veiculo.Cor);
        veiculo.Excluir(Agora.AddMinutes(2));
        Assert.ThrowsExactly<InvalidOperationException>(() => veiculo.Alterar(dados, Agora.AddMinutes(3)));
        var novo = Veiculo.Novo(veiculo.EmpresaId, dados, Agora.AddMinutes(3));
        Assert.AreNotEqual(id, novo.Id);
    }

    [TestMethod]
    public void RejeitaDadosInvalidosSemAlteracaoParcial()
    {
        var dados = Dados;
        var veiculo = Veiculo.Novo(Guid.NewGuid(), dados, Agora);
        var invalidos = new[]
        {
            dados with { Placa = "" }, dados with { Placa = "----" }, dados with { Placa = new string('A', 17) },
            dados with { Marca = "" }, dados with { Marca = new string('A', 101) },
            dados with { Cor = "" }, dados with { Cor = new string('A', 101) },
            dados with { Modelo = "" }, dados with { Modelo = new string('A', 101) },
            dados with { CapacidadeLitros = 0 }, dados with { CapacidadeLitros = -1 },
            dados with { CapacidadeLitros = 0.0001m }, dados with { CapacidadeLitros = 1000000000m },
            dados with { CapacidadeLitros = 1.0000m },
            dados with { Ano = 0 }, dados with { GrupoId = Guid.Empty }, dados with { Combustivel = (TipoCombustivel)99 },
        };
        foreach (var invalido in invalidos)
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => veiculo.Alterar(invalido, Agora.AddMinutes(1)));
            Assert.AreEqual(dados.Placa, veiculo.Placa);
            Assert.AreEqual(dados.CapacidadeLitros, veiculo.CapacidadeLitros);
            Assert.AreEqual(Agora, veiculo.AlteradoEmUtc);
        }
    }

    [TestMethod]
    public void AceitaLimitesDeTextoECapacidadeSemArredondar()
    {
        var dados = Dados with
        {
            Placa = new string('A', 16), Marca = new string('M', 100),
            Cor = new string('C', 100), Modelo = new string('V', 100), CapacidadeLitros = 999999999.999m,
        };
        var veiculo = Veiculo.Novo(Guid.NewGuid(), dados, Agora);
        Assert.AreEqual(dados.Placa, veiculo.Placa);
        Assert.AreEqual(dados.Modelo, veiculo.Modelo);
        Assert.AreEqual(dados.CapacidadeLitros, veiculo.CapacidadeLitros);
    }
}
