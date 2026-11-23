using Locadora.Application.Modulos.Pessoas;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.Pessoas;

[TestClass]
public sealed class CasosPessoasTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Token = CancellationToken.None;

    [TestMethod]
    public async Task CriacaoPfEPjRespeitaCamposEEmailOpcionalEAuditaDentroDaUnidade()
    {
        var ambiente = new Ambiente();
        var pf = await ambiente.Casos.CriarClienteAsync(Pf(), Token);
        var pj = await ambiente.Casos.CriarClienteAsync(Pj(), Token);

        Assert.IsNull(pf.Email);
        Assert.IsNull(pj.Email);
        Assert.AreEqual(ambiente.EmpresaId, pf.EmpresaId);
        Assert.AreEqual(TipoCliente.PF, pf.Tipo);
        Assert.AreEqual(TipoCliente.PJ, pj.Tipo);
        Assert.AreEqual(2, ambiente.Unidade.Chamadas);
        Assert.AreEqual(2, ambiente.Auditoria.Eventos.Count);
        Assert.IsFalse(ambiente.Auditoria.Eventos[0].Campos.Contains("cnpj"));
        Assert.IsFalse(ambiente.Auditoria.Eventos[1].Campos.Contains("cpf"));
    }

    [TestMethod]
    public async Task CondutorAtivoBloqueiaExclusaoAntesDeAlterarClienteVinculoOuAuditoria()
    {
        var ambiente = new Ambiente();
        var pf = ambiente.Cliente(Pf());
        var pj = ambiente.Cliente(Pj());
        var vinculo = ambiente.Vinculo(pf, pj);
        ambiente.Condutor(Dados(pf.Id));

        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.ExcluirClienteAsync(pf.Id, Versao(pf), Token));

        Assert.AreEqual("cliente_com_condutores_ativos", erro.Codigo);
        Assert.IsNull(pf.ExcluidoEmUtc);
        Assert.IsNull(vinculo.ExcluidoEmUtc);
        Assert.IsNull(pj.ExcluidoEmUtc);
        Assert.AreEqual(0, ambiente.Auditoria.Eventos.Count);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ExclusaoPermitidaEncerraVinculosSemExcluirOutroCliente(bool excluirPf)
    {
        var ambiente = new Ambiente();
        var pf = ambiente.Cliente(Pf());
        var pj = ambiente.Cliente(Pj());
        var vinculo = ambiente.Vinculo(pf, pj);
        var alvo = excluirPf ? pf : pj;
        var outro = excluirPf ? pj : pf;

        await ambiente.Casos.ExcluirClienteAsync(alvo.Id, Versao(alvo), Token);

        Assert.IsNotNull(alvo.ExcluidoEmUtc);
        Assert.AreEqual(alvo.ExcluidoEmUtc, vinculo.ExcluidoEmUtc);
        Assert.IsNull(outro.ExcluidoEmUtc);
        Assert.AreEqual(2, ambiente.Auditoria.Eventos.Count);
        Assert.IsTrue(ambiente.Auditoria.Eventos.All(item => item.Acao == AcaoCadastro.Exclusao));
    }

    [TestMethod]
    public async Task VinculosRejeitamTiposIncompativeisDuplicidadeEReferenciaExterna()
    {
        var ambiente = new Ambiente();
        var pf = ambiente.Cliente(Pf());
        var pj = ambiente.Cliente(Pj());
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() => ambiente.Casos.VincularAsync(pf.Id, pf.Id, Token));
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => ambiente.Casos.VincularAsync(pf.Id, Guid.NewGuid(), Token));
        var vinculo = await ambiente.Casos.VincularAsync(pf.Id, pj.Id, Token);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() => ambiente.Casos.VincularAsync(pf.Id, pj.Id, Token));
        await ambiente.Casos.DesvincularAsync(pf.Id, vinculo.Id, Versao(vinculo), Token);
        Assert.IsNull(pf.ExcluidoEmUtc);
        Assert.IsNull(pj.ExcluidoEmUtc);
        Assert.IsNotNull(vinculo.ExcluidoEmUtc);
    }

    [TestMethod]
    public async Task ProprioCondutorExigeCpfECnhCoerentes()
    {
        var ambiente = new Ambiente();
        var cliente = ambiente.Cliente(Pf());
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            ambiente.Casos.CriarCondutorAsync(Dados(cliente.Id) with { Cnh = "OUTRA-CNH" }, true, Token));
        await Assert.ThrowsExactlyAsync<ValidacaoCadastroException>(() =>
            ambiente.Casos.CriarCondutorAsync(Dados(cliente.Id) with { Cpf = "00000000191" }, true, Token));
        var condutor = await ambiente.Casos.CriarCondutorAsync(Dados(cliente.Id), true, Token);
        Assert.AreEqual(cliente.CpfNormalizado, condutor.CpfNormalizado);
        Assert.AreEqual(cliente.CnhNormalizada, condutor.CnhNormalizada);
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.CriarCondutorAsync(Dados(cliente.Id), true, Token));
    }

    [TestMethod]
    public async Task AlteracaoDeCnhDoClienteCoordenaCondutorEAuditaSomenteCamposAlterados()
    {
        var ambiente = new Ambiente();
        var cliente = ambiente.Cliente(Pf());
        var condutor = ambiente.Condutor(Dados(cliente.Id));

        await ambiente.Casos.AlterarClienteAsync(cliente.Id, Pf() with { Cnh = "NOVA-CNH" }, Versao(cliente), Token);

        Assert.AreEqual("NOVACNH", cliente.CnhNormalizada);
        Assert.AreEqual(cliente.CnhNormalizada, condutor.CnhNormalizada);
        Assert.AreEqual(2, ambiente.Auditoria.Eventos.Count);
        Assert.IsTrue(ambiente.Auditoria.Eventos.All(item => item.Campos.SequenceEqual(new[] { "cnh" })));
        await ambiente.Casos.AlterarClienteAsync(cliente.Id, Pf() with { Cnh = "NOVA-CNH" }, Versao(cliente), Token);
        Assert.AreEqual(2, ambiente.Auditoria.Eventos.Count);
    }

    [TestMethod]
    public async Task PontoDeExtensaoImpedeAlteracaoCoordenadaSeCondutorTiverAluguelAberto()
    {
        var ambiente = new Ambiente();
        var cliente = ambiente.Cliente(Pf());
        var condutor = ambiente.Condutor(Dados(cliente.Id));
        ambiente.Alugueis.CondutorBloqueado = true;

        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.AlterarClienteAsync(cliente.Id, Pf() with { Cnh = "OUTRA-CNH" }, Versao(cliente), Token));

        Assert.AreEqual(Pf().Cnh, cliente.Cnh);
        Assert.AreEqual(Dados(cliente.Id).Cnh, condutor.Cnh);
        Assert.AreEqual(0, ambiente.Auditoria.Eventos.Count);
    }

    [TestMethod]
    [DataRow("cpf")]
    [DataRow("cnh")]
    [DataRow("validadeCnh")]
    [DataRow("clienteId")]
    public async Task PontoDeExtensaoProtegeCamposDoCondutorMasPermiteContato(string campo)
    {
        var ambiente = new Ambiente();
        var cliente = ambiente.Cliente(Pf());
        var condutor = ambiente.Condutor(Dados(cliente.Id));
        var outroCliente = ambiente.Cliente(Pj());
        ambiente.Alugueis.CondutorBloqueado = true;
        var alteracao = campo switch
        {
            "cpf" => Dados(cliente.Id) with { Cpf = "00000000191" },
            "cnh" => Dados(cliente.Id) with { Cnh = "OUTRA-CNH" },
            "validadeCnh" => Dados(cliente.Id) with { ValidadeCnh = new DateOnly(2029, 1, 1) },
            _ => Dados(outroCliente.Id),
        };

        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.AlterarCondutorAsync(condutor.Id, alteracao, false, Versao(condutor), Token));
        await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.ExcluirCondutorAsync(condutor.Id, Versao(condutor), Token));
        await ambiente.Casos.AlterarCondutorAsync(condutor.Id, Dados(cliente.Id) with { Telefone = "111111111" }, true, Versao(condutor), Token);

        Assert.AreEqual("111111111", condutor.Telefone);
        Assert.AreEqual(Dados(cliente.Id).ValidadeCnh, condutor.ValidadeCnh);
        Assert.IsNull(condutor.ExcluidoEmUtc);
        Assert.IsTrue(ambiente.Auditoria.Eventos.Single().Campos.SequenceEqual(new[] { "telefone" }));
    }

    [TestMethod]
    public async Task PontoDeExtensaoDoClienteBloqueiaExclusaoAntesDeEncerrarVinculos()
    {
        var ambiente = new Ambiente();
        var pf = ambiente.Cliente(Pf());
        var pj = ambiente.Cliente(Pj());
        var vinculo = ambiente.Vinculo(pf, pj);
        ambiente.Alugueis.ClienteBloqueado = true;

        var erro = await Assert.ThrowsExactlyAsync<ConflitoCadastroException>(() =>
            ambiente.Casos.ExcluirClienteAsync(pf.Id, Versao(pf), Token));

        Assert.AreEqual("cliente_com_aluguel_aberto", erro.Codigo);
        Assert.IsNull(pf.ExcluidoEmUtc);
        Assert.IsNull(vinculo.ExcluidoEmUtc);
        Assert.AreEqual(0, ambiente.Auditoria.Eventos.Count);
    }

    [TestMethod]
    public async Task IsolamentoEPrecondicaoNaoDeixamEfeito()
    {
        var ambiente = new Ambiente();
        var cliente = ambiente.Cliente(Pf());
        await Assert.ThrowsExactlyAsync<CadastroNaoEncontradoException>(() => ambiente.Casos.ClienteAsync(Guid.NewGuid(), Token));
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() => ambiente.Casos.ExcluirClienteAsync(cliente.Id, null, Token));
        await Assert.ThrowsExactlyAsync<PrecondicaoCadastroException>(() => ambiente.Casos.ExcluirClienteAsync(cliente.Id, "\"antiga\"", Token));
        Assert.IsNull(cliente.ExcluidoEmUtc);
        Assert.AreEqual(0, ambiente.Auditoria.Eventos.Count);
    }

    private static string Versao(EntidadeCadastro entidade) => VersaoCadastro.Etag(entidade.RowVersion);
    private static DadosCliente Pf() => new(TipoCliente.PF, "Pessoa sintética", "123.000.000-32", null,
        "RG-SINTETICO", "CNH-SINTETICA", "000000000", new Endereco("Estado", "Cidade", "Bairro", "Rua", "S/N"), null);
    private static DadosCliente Pj() => new(TipoCliente.PJ, "PJ sintética", null, "12.ABC.345/01DE-35", null, null,
        "000000000", new Endereco("Estado", "Cidade", "Bairro", "Rua", "S/N"), null);
    private static DadosCondutor Dados(Guid clienteId) => new(clienteId, "Condutor sintético", "condutor@example.invalid",
        "123.000.000-32", "CNH-SINTETICA", new DateOnly(2028, 1, 1), "000000000");

    private sealed class Ambiente : IContextoCadastro
    {
        public Guid EmpresaId { get; } = Guid.NewGuid();
        public RepositorioMemoria Repositorio { get; } = new();
        public UnidadeControlada Unidade { get; } = new();
        public AuditoriaControlada Auditoria { get; }
        public AlugueisControlados Alugueis { get; } = new();
        public CasosPessoas Casos { get; }

        public Ambiente()
        {
            Auditoria = new(Unidade);
            Casos = new(Repositorio, Unidade, Auditoria, this, Alugueis, new RelogioFixo());
        }

        public Cliente Cliente(DadosCliente dados)
        {
            var cliente = ClientePersistido(Locadora.Domain.Modulos.Pessoas.Cliente.Novo(EmpresaId, dados, Agora));
            Repositorio.Adicionar(cliente);
            return cliente;
        }

        public Condutor Condutor(DadosCondutor dados)
        {
            var condutor = ClientePersistido(Locadora.Domain.Modulos.Pessoas.Condutor.Novo(EmpresaId, dados, Agora));
            Repositorio.Adicionar(condutor);
            return condutor;
        }

        public VinculoCliente Vinculo(Cliente pf, Cliente pj)
        {
            var vinculo = ClientePersistido(VinculoCliente.Novo(pf, pj, Agora));
            Repositorio.Adicionar(vinculo);
            return vinculo;
        }
    }

    private static T ClientePersistido<T>(T entidade) where T : EntidadeCadastro
    {
        // Token preparado para unidade de aplicação; não simula prova de rowversion SQL.
        typeof(EntidadeCadastro).GetProperty(nameof(EntidadeCadastro.RowVersion))!.SetValue(entidade, new byte[8]);
        return entidade;
    }

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed class UnidadeControlada : IUnidadeTrabalhoEmpresa
    {
        public bool Ativa { get; private set; }
        public int Chamadas { get; private set; }
        public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento)
        {
            Chamadas++;
            Ativa = true;
            try { return await comando(cancelamento); }
            finally { Ativa = false; }
        }
    }

    private sealed class AuditoriaControlada(UnidadeControlada unidade) : IAuditoriaCadastro
    {
        public List<(AcaoCadastro Acao, TipoCadastro Tipo, Guid Id, string[] Campos)> Eventos { get; } = [];
        public void Registrar(AcaoCadastro acao, TipoCadastro entidade, Guid entidadeId, params string[] campos)
        {
            Assert.IsTrue(unidade.Ativa);
            Eventos.Add((acao, entidade, entidadeId, campos));
        }
    }

    private sealed class AlugueisControlados : IVinculosAluguelPessoas
    {
        public bool CondutorBloqueado { get; set; }
        public bool ClienteBloqueado { get; set; }
        public Task<bool> ClientePossuiAluguelAbertoAsync(Guid clienteId, CancellationToken cancelamento) => Task.FromResult(ClienteBloqueado);
        public Task<bool> CondutorPossuiAluguelAbertoAsync(Guid condutorId, CancellationToken cancelamento) => Task.FromResult(CondutorBloqueado);
    }

    private sealed class RepositorioMemoria : IRepositorioPessoas
    {
        private readonly List<Cliente> _clientes = [];
        private readonly List<Condutor> _condutores = [];
        private readonly List<VinculoCliente> _vinculos = [];
        private IEnumerable<Cliente> Clientes => _clientes.Where(item => item.ExcluidoEmUtc is null);
        private IEnumerable<Condutor> Condutores => _condutores.Where(item => item.ExcluidoEmUtc is null);
        public Task<Pagina<Cliente>> ListarClientesAsync(Paginacao p, CancellationToken c) =>
            Task.FromResult(new Pagina<Cliente>(Clientes.Skip(p.Deslocamento).Take(p.PageSize).ToArray(), p.Page, p.PageSize, Clientes.Count()));
        public Task<Pagina<Condutor>> ListarCondutoresAsync(Paginacao p, CancellationToken c) =>
            Task.FromResult(new Pagina<Condutor>(Condutores.Skip(p.Deslocamento).Take(p.PageSize).ToArray(), p.Page, p.PageSize, Condutores.Count()));
        public Task<Cliente?> ClienteAsync(Guid id, CancellationToken c) => Task.FromResult(Clientes.SingleOrDefault(item => item.Id == id));
        public Task<Cliente?> ClientePorCpfAsync(string cpf, CancellationToken c) => Task.FromResult(Clientes.SingleOrDefault(item => item.CpfNormalizado == cpf));
        public Task<Condutor?> CondutorAsync(Guid id, CancellationToken c) => Task.FromResult(Condutores.SingleOrDefault(item => item.Id == id));
        public Task<Condutor?> CondutorPorCpfAsync(string cpf, CancellationToken c) => Task.FromResult(Condutores.SingleOrDefault(item => item.CpfNormalizado == cpf));
        public Task<bool> DocumentoClienteEmUsoAsync(TipoCliente tipo, string doc, Guid? exceto, CancellationToken c) =>
            Task.FromResult(Clientes.Any(item => item.Tipo == tipo && item.Id != exceto && (item.CpfNormalizado ?? item.CnpjNormalizado) == doc));
        public Task<bool> CpfCondutorEmUsoAsync(string cpf, Guid? exceto, CancellationToken c) =>
            Task.FromResult(Condutores.Any(item => item.CpfNormalizado == cpf && item.Id != exceto));
        public Task<bool> PossuiCondutoresAtivosAsync(Guid id, CancellationToken c) => Task.FromResult(Condutores.Any(item => item.ClienteId == id));
        public Task<IReadOnlyList<VinculoCliente>> VinculosAsync(Guid id, CancellationToken c) =>
            Task.FromResult<IReadOnlyList<VinculoCliente>>(_vinculos.Where(item => item.ExcluidoEmUtc is null && (item.ClientePfId == id || item.ClientePjId == id)).ToArray());
        public Task<VinculoCliente?> VinculoAsync(Guid id, CancellationToken c) =>
            Task.FromResult(_vinculos.SingleOrDefault(item => item.Id == id && item.ExcluidoEmUtc is null));
        public void Adicionar(Cliente cliente) => _clientes.Add(ClientePersistido(cliente));
        public void Adicionar(Condutor condutor) => _condutores.Add(ClientePersistido(condutor));
        public void Adicionar(VinculoCliente vinculo) => _vinculos.Add(ClientePersistido(vinculo));
    }
}
