using System.Text.Json;
using Locadora.Application.Modulos.Pessoas;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.Pessoas;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Locadora.IntegrationTests.Modulos.Pessoas;

[TestClass]
public sealed class CasosPessoasSqlTests
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [TestMethod]
    public async Task CasosPersistemPfPjCondutorEVinculoComAuditoriaSemValoresPessoais()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var pf = await ambiente.Casos.CriarClienteAsync(Pf(), Token);
        var pj = await ambiente.Casos.CriarClienteAsync(Pj(), Token);
        var vinculo = await ambiente.Casos.VincularAsync(pf.Id, pj.Id, Token);
        var condutor = await ambiente.Casos.CriarCondutorAsync(Dados(pf.Id), true, Token);
        var clientes = await ambiente.Casos.ListarClientesAsync(new Paginacao(), Token);
        var condutores = await ambiente.Casos.ListarCondutoresAsync(new Paginacao(), Token);

        Assert.AreEqual(2, clientes.Total);
        Assert.AreEqual(1, condutores.Total);
        Assert.IsNull((await ambiente.Casos.ClienteAsync(pf.Id, Token)).Email);
        Assert.AreEqual(condutor.Id, condutores.Items.Single().Id);
        Assert.AreEqual(8, vinculo.RowVersion.Length);
        var eventos = await ambiente.Banco.AuditoriasCadastro.ToListAsync();
        Assert.AreEqual(4, eventos.Count);
        Assert.IsTrue(eventos.All(item => item.EmpresaId == ambiente.Contexto.EmpresaId && item.ContaId == ambiente.Contexto.ContaId));
        Assert.IsTrue(eventos.All(item => item.CorrelacaoId == ambiente.Contexto.CorrelacaoId));
        var serializado = JsonSerializer.Serialize(eventos);
        foreach (var valor in new[] { Pf().Cpf!, Pf().Rg!, Pf().Cnh!, Pf().Telefone, Pj().Cnpj!, Dados(pf.Id).Email, Pf().Nome })
        {
            Assert.DoesNotContain(serializado, valor);
        }
    }

    [TestMethod]
    public async Task ClienteComCondutorAtivoERejeitadoETransferenciaPermiteEncerrarSomenteSeusVinculos()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var pf = await ambiente.Casos.CriarClienteAsync(Pf(), Token);
        var pj = await ambiente.Casos.CriarClienteAsync(Pj(), Token);
        var vinculo = await ambiente.Casos.VincularAsync(pf.Id, pj.Id, Token);
        var condutor = await ambiente.Casos.CriarCondutorAsync(Dados(pf.Id), true, Token);
        var antes = await ambiente.Banco.AuditoriasCadastro.CountAsync();

        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.ExcluirClienteAsync(pf.Id, Versao(pf), Token));
        Assert.AreEqual("cliente_com_condutores_ativos", erro.Codigo);
        Assert.AreEqual(antes, await ambiente.Banco.AuditoriasCadastro.CountAsync());
        Assert.IsNotNull(await ambiente.Casos.VinculoAsync(pf.Id, vinculo.Id, Token));

        await ambiente.Casos.AlterarCondutorAsync(condutor.Id, Dados(pj.Id), false, Versao(condutor), Token);
        await ambiente.Casos.ExcluirClienteAsync(pf.Id, Versao(pf), Token);

        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => ambiente.Casos.ClienteAsync(pf.Id, Token));
        Assert.IsNotNull(await ambiente.Casos.ClienteAsync(pj.Id, Token));
        Assert.AreEqual(pj.Id, (await ambiente.Casos.CondutorAsync(condutor.Id, Token)).ClienteId);
        Assert.AreEqual(0, (await ambiente.Casos.VinculosAsync(pj.Id, Token)).Count);
        Assert.AreEqual(2, await ambiente.Banco.Clientes.IgnoreQueryFilters()
            .CountAsync(item => item.EmpresaId == ambiente.Contexto.EmpresaId));
    }

    [TestMethod]
    public async Task FalhaAposSaveChangesDesfazClienteVinculosEAuditoriasJuntos()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var pf = await ambiente.Casos.CriarClienteAsync(Pf(), Token);
        var pj = await ambiente.Casos.CriarClienteAsync(Pj(), Token);
        var vinculo = await ambiente.Casos.VincularAsync(pf.Id, pj.Id, Token);
        var antes = await ambiente.Banco.AuditoriasCadastro.CountAsync();
        var falha = new AuditoriaQueFalha(ambiente.Banco, ambiente.Auditoria);
        var casos = ambiente.CriarCasos(falha);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            casos.ExcluirClienteAsync(pf.Id, Versao(pf), Token));

        Assert.IsTrue(falha.GravouDentroDaTransacao);
        await using var leitura = AmbienteIntegracao.CriarContexto(contexto: ambiente.Contexto);
        Assert.IsTrue(await leitura.Clientes.AnyAsync(item => item.Id == pf.Id));
        Assert.IsTrue(await leitura.Clientes.AnyAsync(item => item.Id == pj.Id));
        Assert.IsTrue(await leitura.VinculosClientes.AnyAsync(item => item.Id == vinculo.Id));
        Assert.AreEqual(antes, await leitura.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task AlteracaoDoClienteCoordenaCnhDoCondutorEAuditaSomenteAlteracoes()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var pf = await ambiente.Casos.CriarClienteAsync(Pf(), Token);
        var condutor = await ambiente.Casos.CriarCondutorAsync(Dados(pf.Id), true, Token);
        var atual = await ambiente.Casos.AlterarClienteAsync(pf.Id, Pf() with { Cnh = "NOVA-CNH" }, Versao(pf), Token);
        var novoCondutor = await ambiente.Casos.CondutorAsync(condutor.Id, Token);
        Assert.AreEqual(atual.CnhNormalizada, novoCondutor.CnhNormalizada);
        Assert.IsFalse(condutor.RowVersion.SequenceEqual(novoCondutor.RowVersion));
        var eventos = await ambiente.Banco.AuditoriasCadastro.Where(item => item.Acao == AcaoCadastro.Alteracao).ToListAsync();
        Assert.AreEqual(2, eventos.Count);
        Assert.IsTrue(eventos.All(item => item.CamposAlterados == "cnh"));

        var antes = await ambiente.Banco.AuditoriasCadastro.CountAsync();
        var semMudanca = await ambiente.Casos.AlterarClienteAsync(pf.Id, Pf() with { Cnh = "NOVA-CNH" }, Versao(atual), Token);
        Assert.IsTrue(atual.RowVersion.SequenceEqual(semMudanca.RowVersion));
        Assert.AreEqual(antes, await ambiente.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task IsolamentoImpedeConsultaEscritaEVinculoComIdentificadoresDaOutraEmpresa()
    {
        await using var a = await Ambiente.CriarAsync();
        await using var b = await Ambiente.CriarAsync();
        var pfA = await a.Casos.CriarClienteAsync(Pf(), Token);
        var pfB = await b.Casos.CriarClienteAsync(Pf(), Token);
        var pjB = await b.Casos.CriarClienteAsync(Pj(), Token);
        var condutorB = await b.Casos.CriarCondutorAsync(Dados(pfB.Id), true, Token);
        var vinculoB = await b.Casos.VincularAsync(pfB.Id, pjB.Id, Token);

        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Casos.ClienteAsync(pfB.Id, Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Casos.CondutorAsync(condutorB.Id, Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Casos.ExcluirClienteAsync(pfB.Id, Versao(pfB), Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Casos.VincularAsync(pfA.Id, pjB.Id, Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Casos.CriarCondutorAsync(Dados(pfB.Id), false, Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => a.Casos.DesvincularAsync(pfA.Id, vinculoB.Id, Versao(vinculoB), Token));
        Assert.AreEqual(1, (await a.Casos.ListarClientesAsync(new Paginacao(), Token)).Total);
        Assert.AreEqual(2, (await b.Casos.ListarClientesAsync(new Paginacao(), Token)).Total);
        Assert.AreEqual(1, await a.Banco.AuditoriasCadastro.CountAsync());
    }

    [TestMethod]
    public async Task DuplicidadeNaoSobrescreveEExclusaoPermiteNovaIdentidadeSemTransferirVinculos()
    {
        await using var ambiente = await Ambiente.CriarAsync();
        var pf = await ambiente.Casos.CriarClienteAsync(Pf(), Token);
        var condutor = await ambiente.Casos.CriarCondutorAsync(Dados(pf.Id), true, Token);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => ambiente.Casos.CriarClienteAsync(Pf(), Token));
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => ambiente.Casos.CriarCondutorAsync(Dados(pf.Id), true, Token));
        await ambiente.Casos.ExcluirCondutorAsync(condutor.Id, Versao(condutor), Token);
        await ambiente.Casos.ExcluirClienteAsync(pf.Id, Versao(pf), Token);
        var novo = await ambiente.Casos.CriarClienteAsync(Pf(), Token);
        var novoCondutor = await ambiente.Casos.CriarCondutorAsync(Dados(novo.Id), true, Token);
        Assert.AreNotEqual(pf.Id, novo.Id);
        Assert.AreNotEqual(condutor.Id, novoCondutor.Id);
        Assert.AreEqual(novo.Id, novoCondutor.ClienteId);
        Assert.AreEqual(0, (await ambiente.Casos.VinculosAsync(novo.Id, Token)).Count);
    }

    private static string Versao(EntidadeCadastro entidade) => VersaoCadastro.Etag(entidade.RowVersion);
    private static DadosCliente Pf() => new(TipoCliente.PF, "Pessoa sintética", "123.000.000-32", null, "RG-SINTETICO",
        "CNH-SINTETICA", "000000000", new Endereco("Estado", "Cidade", "Bairro", "Rua", "S/N"), null);
    private static DadosCliente Pj() => new(TipoCliente.PJ, "PJ sintética", null, "12.ABC.345/01DE-35", null, null,
        "000000000", new Endereco("Estado", "Cidade", "Bairro", "Rua", "S/N"), null);
    private static DadosCondutor Dados(Guid id) => new(id, "Condutor sintético", "condutor@example.invalid",
        "123.000.000-32", "CNH-SINTETICA", new DateOnly(2028, 1, 1), "000000000");

    private sealed class Ambiente : IAsyncDisposable
    {
        public ContextoEmpresa Contexto { get; }
        public LocadoraDbContext Banco { get; }
        public RegistroAuditoriaCadastro Auditoria { get; }
        public CasosPessoas Casos { get; }

        private Ambiente(ContextoEmpresa contexto, LocadoraDbContext banco)
        {
            Contexto = contexto;
            Banco = banco;
            Auditoria = new RegistroAuditoriaCadastro(banco, contexto, TimeProvider.System);
            Casos = CriarCasos(Auditoria);
        }

        public CasosPessoas CriarCasos(IAuditoriaCadastro auditoria) => new(
            new RepositorioPessoas(Banco),
            new UnidadeTrabalhoEmpresa(Banco, Contexto, NullLogger<UnidadeTrabalhoEmpresa>.Instance),
            auditoria, Contexto, new VinculosAluguelPessoasFase2(), TimeProvider.System);

        public static async Task<Ambiente> CriarAsync()
        {
            var contexto = new ContextoEmpresa { PermiteCriarEmpresa = true, CorrelacaoId = Guid.NewGuid() };
            var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto);
            var empresa = Empresa.Nova();
            var nome = "pessoas_" + Guid.NewGuid().ToString("N");
            var conta = new UsuarioConta { Id = Guid.NewGuid(), EmpresaId = empresa.Id, UserName = nome, NormalizedUserName = nome.ToUpperInvariant() };
            contexto.EmpresaId = empresa.Id;
            contexto.ContaId = conta.Id;
            banco.AddRange(empresa, conta);
            await banco.SaveChangesAsync();
            contexto.PermiteCriarEmpresa = false;
            return new Ambiente(contexto, banco);
        }

        public ValueTask DisposeAsync() => Banco.DisposeAsync();
    }

    private sealed class AuditoriaQueFalha(LocadoraDbContext banco, IAuditoriaCadastro original) : IAuditoriaCadastro
    {
        public bool GravouDentroDaTransacao { get; private set; }
        public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid id, params string[] campos)
        {
            original.Registrar(acao, entidade, id, campos);
            if (acao == AcaoCadastro.Exclusao && entidade == TipoCadastro.Cliente)
            {
                Assert.IsNotNull(banco.Database.CurrentTransaction);
                banco.SaveChanges();
                GravouDentroDaTransacao = true;
                throw new InvalidOperationException("Falha controlada depois de gravar e antes do commit.");
            }
        }
    }
}
