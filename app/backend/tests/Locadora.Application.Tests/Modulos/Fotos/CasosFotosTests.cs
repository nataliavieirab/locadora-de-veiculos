using Locadora.Application.Modulos.Fotos;
using Locadora.Application.Modulos.Transversal;

namespace Locadora.Application.Tests.Modulos.Fotos;

[TestClass]
public sealed class CasosFotosTests
{
    [TestMethod]
    public async Task SessaoNegadaImpedeProcessarPersistirOuEnviarBytes()
    {
        var casos = new CasosFotos(null!, null!, null!, new Unidade(), new SessaoNegada(),
            null!, null!, null!, null!, TimeProvider.System);
        await Assert.ThrowsExactlyAsync<SessaoFotoInvalidaException>(() =>
            casos.EnviarAsync(Guid.NewGuid(), null, Stream.Null, "image/png", default));
        await Assert.ThrowsExactlyAsync<SessaoFotoInvalidaException>(() =>
            casos.PrepararAsync(Guid.NewGuid(), null, new([1], "image/png", 1, 1), default));
        await Assert.ThrowsExactlyAsync<SessaoFotoInvalidaException>(() =>
            casos.RemoverAsync(Guid.NewGuid(), null, default));
    }

    private sealed class Unidade : IUnidadeTrabalhoEmpresa
    {
        public Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> comando, CancellationToken cancelamento)
            => comando(cancelamento);
    }
    private sealed class SessaoNegada : ISessaoFotos
    {
        public Guid AtorId => throw new InvalidOperationException();
        public string Correlacao => throw new InvalidOperationException();
        public Task ExigirAsync(CancellationToken cancelamento) => throw new SessaoFotoInvalidaException();
    }
}
