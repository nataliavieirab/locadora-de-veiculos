using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Tests.Modulos.Fotos;

[TestClass]
public sealed class FotosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static Veiculo Veiculo(Guid? empresa = null) => Domain.Modulos.FrotaPrecos.Veiculo.Novo(
        empresa ?? Guid.NewGuid(), new("FIC1A01", "Sintética", "Azul", "Modelo", TipoCombustivel.Gasolina,
            50, 2026, Guid.NewGuid()), Agora);
    private static FotoObjeto Foto(Veiculo veiculo) => FotoObjeto.Preparar(veiculo, "image/png", 100, 10, 10,
        new byte[32], new byte[8], ProvedorFoto.Local, "volume-local", Guid.NewGuid(), "ensaio", Agora);

    [TestMethod]
    public void AtivacaoTrocaERemocaoMantemUmaReferenciaEAvancamInstanteDaRaiz()
    {
        var veiculo = Veiculo();
        var primeira = Foto(veiculo);
        var segunda = Foto(veiculo);
        Assert.IsNull(veiculo.FotoAtivaId);
        veiculo.DefinirFoto(primeira, null, Agora.AddSeconds(1));
        Assert.AreEqual(primeira.Id, veiculo.FotoAtivaId);
        Assert.AreEqual(EstadoFoto.Ativa, primeira.Estado);
        Assert.ThrowsExactly<InvalidOperationException>(() => veiculo.DefinirFoto(segunda, null, Agora.AddSeconds(2)));
        veiculo.DefinirFoto(segunda, primeira, Agora.AddSeconds(2));
        Assert.AreEqual(EstadoFoto.Inativa, primeira.Estado);
        Assert.AreEqual(segunda.Id, veiculo.FotoAtivaId);
        Assert.ThrowsExactly<InvalidOperationException>(() => primeira.Abandonar(Agora.AddSeconds(3)));
        veiculo.RemoverFoto(segunda, Agora.AddSeconds(3));
        Assert.IsNull(veiculo.FotoAtivaId);
        Assert.AreEqual(EstadoFoto.Inativa, segunda.Estado);
        Assert.AreEqual(Agora.AddSeconds(3), veiculo.AlteradoEmUtc);
        Assert.AreEqual(0, veiculo.RowVersion.Length); // Geração efetiva é verificada no SQL.
        veiculo.RemoverFoto(null, Agora.AddSeconds(4));
        Assert.AreEqual(Agora.AddSeconds(3), veiculo.AlteradoEmUtc);
    }

    [TestMethod]
    public void NaoAceitaFotoExternaDeOutroVeiculoAbandonadaOuInstanteRetroativo()
    {
        var veiculo = Veiculo();
        foreach (var externa in new[] { Foto(Veiculo()), Foto(Veiculo(veiculo.EmpresaId)) })
            Assert.ThrowsExactly<InvalidOperationException>(() => veiculo.DefinirFoto(externa, null, Agora));
        var foto = Foto(veiculo);
        Assert.ThrowsExactly<InvalidOperationException>(() => veiculo.DefinirFoto(foto, null, Agora.AddSeconds(-1)));
        Assert.IsNull(veiculo.FotoAtivaId);
        Assert.AreEqual(EstadoFoto.Pendente, foto.Estado);
        foto.Abandonar(Agora);
        Assert.ThrowsExactly<InvalidOperationException>(() => veiculo.DefinirFoto(foto, null, Agora));
        veiculo.Excluir(Agora);
        Assert.ThrowsExactly<InvalidOperationException>(() => veiculo.DefinirFoto(Foto(Veiculo()), null, Agora));
    }

    [TestMethod]
    public void ChaveECaminhoSaoGeradosInternamenteEVetoresDeEntradaSaoCopiados()
    {
        var veiculo = Veiculo();
        var hash = new byte[32];
        var versao = new byte[8];
        var foto = FotoObjeto.Preparar(veiculo, "image/jpeg", FotoObjeto.MaximoBytes, 4000, 4000,
            hash, versao, ProvedorFoto.R2, "bucket-sintetico", Guid.NewGuid(), "ensaio", Agora);
        hash[0] = 9; versao[0] = 9;
        Assert.AreEqual((byte)0, foto.Sha256[0]);
        Assert.AreEqual((byte)0, foto.VersaoBase[0]);
        Assert.AreEqual($"{veiculo.EmpresaId:N}/{foto.Id:N}", foto.Chave);
        Assert.AreEqual($"/api/v1/veiculos/{veiculo.Id:D}/foto", foto.Caminho);
        Assert.ThrowsExactly<ArgumentException>(() => FotoObjeto.Preparar(veiculo, "image/png", 100, 10, 10,
            hash, versao, ProvedorFoto.R2, "https://externo.invalid", Guid.NewGuid(), "ensaio", Agora));
    }

    [TestMethod]
    public void RejeitaMetadadosNaoSaneadosELimites()
    {
        var veiculo = Veiculo();
        foreach (var (mime, bytes, largura, altura) in new[]
        {
            ("image/gif", 1, 1, 1), ("image/png", 0, 1, 1), ("image/png", FotoObjeto.MaximoBytes + 1, 1, 1),
            ("image/jpeg", 1, 0, 1), ("image/png", 1, 4001, 4000), ("image/png", 1, int.MaxValue, int.MaxValue)
        })
            Assert.ThrowsExactly<ValidacaoCadastroException>(() => FotoObjeto.Preparar(veiculo, mime, bytes,
                largura, altura, new byte[32], new byte[8], ProvedorFoto.Local, "local", Guid.NewGuid(), "teste", Agora));
    }

    [TestMethod]
    public void RetencaoNaoAdmitePosseNemCompensacaoELeaseProtegeTrabalho()
    {
        var veiculo = Veiculo();
        var foto = Foto(veiculo);
        var trabalho = TrabalhoObjeto.Criar(foto, Agora);
        var token = trabalho.Adquirir(Agora);
        Assert.ThrowsExactly<InvalidOperationException>(() => trabalho.Adquirir(Agora.AddSeconds(1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => trabalho.Concluir(token, foto, Agora));
        foto.Abandonar(Agora);
        Assert.ThrowsExactly<InvalidOperationException>(() => trabalho.Concluir(Guid.NewGuid(), foto, Agora));
        trabalho.ConfirmarEnvio(token, Agora);
        trabalho.Concluir(token, foto, Agora);
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, trabalho.Estado);
        var ativa = Foto(veiculo);
        veiculo.DefinirFoto(ativa, null, Agora);
        var retido = TrabalhoObjeto.Criar(ativa, Agora);
        Assert.AreEqual(TipoTrabalhoObjeto.RemocaoRetida, retido.Tipo);
        Assert.ThrowsExactly<InvalidOperationException>(() => retido.Adquirir(Agora));
    }

    [TestMethod]
    public void RetriesNaoTerminamNaQuintaEBackoffSaturaSemApagarAckOuErro()
    {
        var trabalho = TrabalhoObjeto.Criar(Foto(Veiculo()), Agora);
        var instante = Agora;
        for (var tentativa = 0; tentativa < 40; tentativa++)
        {
            var token = trabalho.Adquirir(instante);
            Assert.ThrowsExactly<InvalidOperationException>(() => trabalho.ExigirPosse(token, instante.AddMinutes(2)));
            trabalho.RegistrarFalha(token, instante);
            Assert.IsTrue(trabalho.ProximoProcessamentoUtc - instante <= TimeSpan.FromDays(1));
            if (tentativa >= 12)
                Assert.AreEqual(TimeSpan.FromDays(1), trabalho.ProximoProcessamentoUtc - instante);
            instante = trabalho.ProximoProcessamentoUtc;
        }
        Assert.AreEqual(40, trabalho.Tentativas);
        Assert.AreEqual(EstadoTrabalhoObjeto.Pendente, trabalho.Estado);
        Assert.AreEqual("armazenamento_indisponivel", trabalho.CodigoFalha);
        var posse = trabalho.Adquirir(instante);
        trabalho.ConfirmarEnvio(posse, instante);
        Assert.AreEqual("armazenamento_indisponivel", trabalho.CodigoFalha);
        trabalho.RegistrarFalha(posse, instante);
        Assert.IsTrue(trabalho.EnvioConfirmado);
        Assert.AreEqual(instante, trabalho.EnvioConfirmadoEmUtc);
        Assert.AreEqual(instante, trabalho.UltimaFalhaEmUtc);
    }

    [TestMethod]
    public void RetencaoComecaNaRemocaoImutavelENaoNaAtivacao()
    {
        var veiculo = Veiculo();
        var foto = Foto(veiculo);
        veiculo.DefinirFoto(foto, null, Agora);
        var trabalho = TrabalhoObjeto.Criar(foto, Agora);
        var remocao = Agora.AddDays(90);
        Assert.IsFalse(foto.ElegivelParaRemocao(remocao));
        Assert.IsNull(foto.RemovidaEmUtc);
        Assert.IsNull(foto.ExcluirAposUtc);
        veiculo.RemoverFoto(foto, remocao);
        trabalho.AgendarRetencao(foto, remocao);
        var limite = remocao.AddDays(30);
        Assert.AreEqual(remocao, foto.RemovidaEmUtc);
        Assert.AreEqual(limite, foto.ExcluirAposUtc);
        Assert.IsFalse(foto.RetencaoLegada);
        Assert.IsFalse(foto.ElegivelParaRemocao(limite.AddTicks(-1)));
        Assert.IsTrue(foto.ElegivelParaRemocao(limite));
        Assert.IsTrue(foto.ElegivelParaRemocao(limite.AddTicks(1)));
        Assert.ThrowsExactly<InvalidOperationException>(() => trabalho.Adquirir(limite.AddTicks(-1), foto));
        Assert.ThrowsExactly<InvalidOperationException>(() => veiculo.DefinirFoto(foto, null, limite));
        veiculo.RemoverFoto(null, limite);
        Assert.AreEqual(remocao, foto.RemovidaEmUtc);
        Assert.AreEqual(limite, foto.ExcluirAposUtc);
        var token = trabalho.Adquirir(limite, foto);
        trabalho.Concluir(token, foto, limite);
        Assert.AreEqual(EstadoTrabalhoObjeto.Concluido, trabalho.Estado);
    }

    [TestMethod]
    public void AtivacaoConverteTrabalhoExistenteEmRetencaoSemDuplicarOuCompensarAtivo()
    {
        var veiculo = Veiculo();
        var foto = Foto(veiculo);
        var trabalho = TrabalhoObjeto.Criar(foto, Agora);
        var id = trabalho.Id;
        var token = trabalho.Adquirir(Agora);
        Assert.ThrowsExactly<InvalidOperationException>(() => trabalho.Reter(token, foto, Agora));
        veiculo.DefinirFoto(foto, null, Agora);
        trabalho.Reter(token, foto, Agora);
        Assert.AreEqual(id, trabalho.Id);
        Assert.AreEqual(TipoTrabalhoObjeto.RemocaoRetida, trabalho.Tipo);
        Assert.AreEqual(EstadoTrabalhoObjeto.Retido, trabalho.Estado);
        Assert.IsNull(trabalho.TokenPosse);
        Assert.IsNull(trabalho.LeaseAteUtc);
        Assert.ThrowsExactly<InvalidOperationException>(() => trabalho.Adquirir(Agora));
    }
}
