using Locadora.Application.Modulos.Acesso.Registro;

namespace Locadora.Application.Tests.Modulos.Acesso.Registro;

[TestClass]
public sealed class RegistroEmpresaTests
{
    [TestMethod]
    public async Task SenhaCurtaResponde400()
    {
        var caso = Criar();
        var resultado = await caso.ExecutarAsync("ana", "ana@exemplo.com", "curta", CancellationToken.None);
        Assert.AreEqual(400, CodigoResultadoRegistro.ParaHttp(resultado));
        var invalido = (ResultadoRegistro.Invalido)resultado;
        Assert.IsTrue(invalido.Erros.Any(erro => erro.Campo == "password"));
    }

    [TestMethod]
    public async Task SenhaLongaResponde400()
    {
        var caso = Criar();
        var resultado = await caso.ExecutarAsync("ana", "ana@exemplo.com", new string('a', 129), CancellationToken.None);
        Assert.AreEqual(400, CodigoResultadoRegistro.ParaHttp(resultado));
    }

    [TestMethod]
    public async Task EmailInvalidoResponde400()
    {
        var caso = Criar();
        var resultado = await caso.ExecutarAsync("ana", "sem-arroba", "abcdefgh", CancellationToken.None);
        Assert.AreEqual(400, CodigoResultadoRegistro.ParaHttp(resultado));
        Assert.IsTrue(((ResultadoRegistro.Invalido)resultado).Erros.Any(erro => erro.Campo == "email"));
    }

    [TestMethod]
    public async Task UsuarioVazioResponde400()
    {
        var caso = Criar();
        var resultado = await caso.ExecutarAsync("   ", "ana@exemplo.com", "abcdefgh", CancellationToken.None);
        Assert.AreEqual(400, CodigoResultadoRegistro.ParaHttp(resultado));
    }

    [TestMethod]
    public async Task UsuarioComEspacoResponde400()
    {
        var caso = Criar();
        var resultado = await caso.ExecutarAsync("ana silva", "ana@exemplo.com", "abcdefgh", CancellationToken.None);
        Assert.AreEqual(400, CodigoResultadoRegistro.ParaHttp(resultado));
    }

    [TestMethod]
    public async Task UsuarioLongoResponde400()
    {
        var caso = Criar();
        var resultado = await caso.ExecutarAsync(new string('a', 257), "ana@exemplo.com", "abcdefgh", CancellationToken.None);
        Assert.AreEqual(400, CodigoResultadoRegistro.ParaHttp(resultado));
    }

    [TestMethod]
    public async Task AdminEadminRespondem409()
    {
        var repositorio = new RepositorioFalso();
        var caso = new CasoRegistroEmpresa(repositorio, repositorio);
        var primeiro = await caso.ExecutarAsync("Admin", "admin@exemplo.com", "abcdefgh", CancellationToken.None);
        var segundo = await caso.ExecutarAsync("admin", "outro@exemplo.com", "abcdefgh", CancellationToken.None);

        Assert.AreEqual(201, CodigoResultadoRegistro.ParaHttp(primeiro));
        Assert.AreEqual(409, CodigoResultadoRegistro.ParaHttp(segundo));
        Assert.AreEqual("username", ((ResultadoRegistro.Conflito)segundo).Campo);
    }

    [TestMethod]
    public async Task EmailRepetidoComUsuarioNovoResponde201()
    {
        var repositorio = new RepositorioFalso();
        var caso = new CasoRegistroEmpresa(repositorio, repositorio);
        var primeiro = await caso.ExecutarAsync("ana", "mesmo@exemplo.com", "abcdefgh", CancellationToken.None);
        var segundo = await caso.ExecutarAsync("bia", "mesmo@exemplo.com", "!!!!!!!!", CancellationToken.None);

        Assert.AreEqual(201, CodigoResultadoRegistro.ParaHttp(primeiro));
        Assert.AreEqual(201, CodigoResultadoRegistro.ParaHttp(segundo));
        Assert.AreNotEqual(((ResultadoRegistro.Criado)primeiro).EmpresaId, ((ResultadoRegistro.Criado)segundo).EmpresaId);
    }

    private static CasoRegistroEmpresa Criar() => new(new RepositorioFalso(), new RepositorioFalso());

    private sealed class RepositorioFalso : IConsultaUsername, IRegistroEmpresa
    {
        private readonly HashSet<string> _usuarios = new(StringComparer.Ordinal);

        public Task<bool> ExisteNormalizadoAsync(string usernameNormalizado, CancellationToken cancelamento)
            => Task.FromResult(_usuarios.Contains(usernameNormalizado));

        public Task<ResultadoRegistro> RegistrarAsync(string username, string email, string password, CancellationToken cancelamento)
        {
            _usuarios.Add(NormalizadorUsuario.Normalizar(username));
            return Task.FromResult<ResultadoRegistro>(new ResultadoRegistro.Criado(Guid.NewGuid(), Guid.NewGuid()));
        }
    }
}
