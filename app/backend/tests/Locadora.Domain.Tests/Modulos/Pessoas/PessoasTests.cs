using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.Pessoas;

[TestClass]
public sealed class PessoasTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static Endereco Endereco => new("Estado sintético", "Cidade sintética", "Bairro sintético", "Rua sintética", "S/N");
    private static DadosCliente Pf => new(TipoCliente.PF, "Pessoa sintética", "123.000.000-32", null, "RG-SINTETICO", "CNH-SINTETICA", "000000000", Endereco, null);
    private static DadosCliente Pj => new(TipoCliente.PJ, "Empresa sintética", null, "98.700.000/0001-07", null, null, "000000000", Endereco, null);
    private static DadosCondutor Dados => new(Guid.NewGuid(), "Condutor sintético", "condutor@example.invalid", "123.000.000-32", "CNH-SINTETICA", new DateOnly(2028, 1, 1), "000000000");

    [TestMethod]
    public void NormalizaCpfSemRefletirDocumentoEmErros()
    {
        Assert.AreEqual("12300000032", Documentos.Cpf("123.000.000-32"));
        foreach (var invalido in new[] { "", "11111111111", "00000000000", "12300000033", "1230000003", "123000000321", "ABC00000032", "123$00000032" })
        {
            var erro = Assert.ThrowsExactly<ValidacaoCadastroException>(() => Documentos.Cpf(invalido));
            Assert.AreEqual("cpf", erro.Campo);
            if (invalido.Length > 0)
            {
                Assert.DoesNotContain(erro.Message, invalido);
            }
        }
    }

    [TestMethod]
    public void AceitaCnpjNumericoEExemploAlfanumericoDoManualDaReceita()
    {
        Assert.AreEqual("98700000000107", Documentos.Cnpj("98.700.000/0001-07"));
        Assert.AreEqual("12ABC34501DE35", Documentos.Cnpj("12.abc.345/01de-35"));
        foreach (var invalido in new[] { "", "00000000000000", "98700000000108", "12ABC34501DE34", "12ABC34501DE3A", "12ÁBC34501DE35", "12ABC34501DE350" })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => Documentos.Cnpj(invalido));
        }
    }

    [TestMethod]
    public void ClientePfExigeDocumentosPropriosEAceitaEmailOpcional()
    {
        var cliente = Cliente.Novo(Guid.NewGuid(), Pf, Agora);
        Assert.AreEqual(TipoCliente.PF, cliente.Tipo);
        Assert.AreEqual(Pf.Cpf, cliente.Cpf);
        Assert.AreEqual("12300000032", cliente.CpfNormalizado);
        Assert.AreEqual("CNHSINTETICA", cliente.CnhNormalizada);
        Assert.IsNull(cliente.Cnpj);
        Assert.IsNull(cliente.CnpjNormalizado);
        Assert.IsNull(cliente.Email);
        foreach (var invalido in new[] { Pf with { Cpf = null }, Pf with { Rg = "" }, Pf with { Cnh = "" }, Pf with { Cnpj = Pj.Cnpj } })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => cliente.Alterar(invalido, Agora.AddMinutes(1)));
        }

        Assert.AreEqual(Pf.Rg, cliente.Rg);
        Assert.AreEqual(Agora, cliente.AlteradoEmUtc);
    }

    [TestMethod]
    public void ClientePjNaoExigeNemAceitaDocumentosExclusivosDePf()
    {
        var cliente = Cliente.Novo(Guid.NewGuid(), Pj, Agora);
        Assert.AreEqual(TipoCliente.PJ, cliente.Tipo);
        Assert.AreEqual("98700000000107", cliente.CnpjNormalizado);
        Assert.IsNull(cliente.Cpf);
        Assert.IsNull(cliente.Rg);
        Assert.IsNull(cliente.Cnh);
        Assert.IsNull(cliente.Email);
        foreach (var invalido in new[] { Pj with { Cnpj = null }, Pj with { Cpf = Pf.Cpf }, Pj with { Rg = "RG" }, Pj with { Cnh = "CNH" } })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => cliente.Alterar(invalido, Agora.AddMinutes(1)));
        }
    }

    [TestMethod]
    public void ClienteValidaCamposComunsSemTruncamentoOuAlteracaoParcial()
    {
        var dados = Pf with { Nome = new string('N', 200), Telefone = new string('1', 32), Rg = new string('R', 64), Cnh = new string('C', 64) };
        var cliente = Cliente.Novo(Guid.NewGuid(), dados, Agora);
        Assert.AreEqual(dados.Nome, cliente.Nome);
        Assert.AreEqual(dados.Rg, cliente.Rg);
        Assert.AreEqual(dados.Cnh, cliente.Cnh);
        foreach (var invalido in new[]
        {
            dados with { Nome = "" }, dados with { Nome = new string('N', 201) }, dados with { Telefone = "" },
            dados with { Telefone = new string('1', 33) }, dados with { Rg = new string('R', 65) },
            dados with { Cnh = new string('C', 65) }, dados with { Email = "invalido" },
            dados with { Email = new string('e', 255) }, dados with { Endereco = null! }, dados with { Tipo = (TipoCliente)99 },
        })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => cliente.Alterar(invalido, Agora.AddMinutes(1)));
            Assert.AreEqual(dados.Nome, cliente.Nome);
            Assert.AreEqual(Agora, cliente.AlteradoEmUtc);
        }

        cliente.Alterar(dados with { Email = "pessoa@example.invalid" }, Agora.AddMinutes(1));
        Assert.AreEqual("pessoa@example.invalid", cliente.Email);
        cliente.Excluir(Agora.AddMinutes(2));
        Assert.ThrowsExactly<InvalidOperationException>(() => cliente.Alterar(dados, Agora.AddMinutes(3)));
    }

    [TestMethod]
    public void EnderecoExigeTodosOsCamposERespeitaLimites()
    {
        var endereco = new Endereco(new string('E', 100), new string('C', 100), new string('B', 100), new string('R', 200), new string('N', 200));
        Assert.AreEqual(100, endereco.Estado.Length);
        Assert.AreEqual(200, endereco.Rua.Length);
        Assert.AreEqual(200, endereco.Numero.Length);
        foreach (var campo in Enumerable.Range(0, 5))
        {
            var valores = new[] { "Estado", "Cidade", "Bairro", "Rua", "S/N" };
            valores[campo] = "";
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => new Endereco(valores[0], valores[1], valores[2], valores[3], valores[4]));
            valores[campo] = new string('A', campo < 3 ? 101 : 201);
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => new Endereco(valores[0], valores[1], valores[2], valores[3], valores[4]));
        }
    }

    [TestMethod]
    public void VinculoExigePfEPjAtivosDaMesmaEmpresa()
    {
        var empresa = Guid.NewGuid();
        var pf = Cliente.Novo(empresa, Pf, Agora);
        var pj = Cliente.Novo(empresa, Pj, Agora);
        var vinculo = VinculoCliente.Novo(pf, pj, Agora);
        Assert.AreEqual(pf.Id, vinculo.ClientePfId);
        Assert.AreEqual(pj.Id, vinculo.ClientePjId);
        Assert.AreEqual(empresa, vinculo.EmpresaId);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => VinculoCliente.Novo(pj, pf, Agora));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => VinculoCliente.Novo(pf, pf, Agora));
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => VinculoCliente.Novo(pf, Cliente.Novo(Guid.NewGuid(), Pj, Agora), Agora));
        pf.Excluir(Agora);
        Assert.ThrowsExactly<ValidacaoCadastroException>(() => VinculoCliente.Novo(pf, pj, Agora));
        vinculo.Excluir(Agora);
        Assert.IsNotNull(vinculo.ExcluidoEmUtc);
        Assert.IsNull(pj.ExcluidoEmUtc);
    }

    [TestMethod]
    public void CondutorExigeCamposPropriosSemImporElegibilidadeDeAluguel()
    {
        var dados = Dados;
        var condutor = Condutor.Novo(Guid.NewGuid(), dados, Agora);
        Assert.AreEqual(dados.ClienteId, condutor.ClienteId);
        Assert.AreEqual("12300000032", condutor.CpfNormalizado);
        Assert.AreEqual("CNHSINTETICA", condutor.CnhNormalizada);
        Assert.AreEqual(dados.ValidadeCnh, condutor.ValidadeCnh);
        foreach (var invalido in new[]
        {
            dados with { ClienteId = Guid.Empty }, dados with { Nome = "" }, dados with { Nome = new string('N', 201) },
            dados with { Email = "" }, dados with { Email = "invalido" }, dados with { Cpf = "" },
            dados with { Cnh = "" }, dados with { Cnh = new string('C', 65) }, dados with { ValidadeCnh = default },
            dados with { Telefone = "" }, dados with { Telefone = new string('1', 33) },
        })
        {
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => condutor.Alterar(invalido, Agora.AddMinutes(1)));
            Assert.AreEqual(dados.Nome, condutor.Nome);
            Assert.AreEqual(Agora, condutor.AlteradoEmUtc);
        }

        condutor.Alterar(dados with { Nome = "Outro nome sintético", ValidadeCnh = new DateOnly(2020, 1, 1) }, Agora.AddMinutes(1));
        Assert.AreEqual("Outro nome sintético", condutor.Nome);
        condutor.Excluir(Agora.AddMinutes(2));
        Assert.ThrowsExactly<InvalidOperationException>(() => condutor.Alterar(dados, Agora.AddMinutes(3)));
    }
}
