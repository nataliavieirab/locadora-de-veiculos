using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public sealed class ComandoIdempotente : IEntidadeEmpresa
{
    private byte[] _versaoOriginal = [];
    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid ContaId { get; private set; }
    public string Operacao { get; private set; } = "";
    public Guid Chave { get; private set; }
    public string HashComando { get; private set; } = "";
    public Guid RevisaoId { get; private set; }
    public Guid? DemonstrativoFinalId { get; private set; }
    public byte[] VersaoOriginal { get => _versaoOriginal.ToArray(); private set => _versaoOriginal = value.ToArray(); }
    public DateTimeOffset ConfirmadoEmUtc { get; private set; }

    private ComandoIdempotente() { }

    public static ComandoIdempotente RegistrarCriacao(Guid empresaId, Guid contaId, Guid chave,
        string hashComando, Guid revisaoId, byte[] versaoOriginal, DateTimeOffset agora)
    {
        ValidacaoCadastro.Referencia(empresaId, "empresaId");
        ValidacaoCadastro.Referencia(contaId, "contaId");
        ValidacaoCadastro.Referencia(chave, "idempotencyKey");
        ValidacaoCadastro.Referencia(revisaoId, "revisaoId");
        if (hashComando.Length != 64 || hashComando.Any(c => !char.IsAsciiHexDigit(c)) || versaoOriginal.Length != 8)
            throw new ArgumentException("O resultado idempotente exige hash e versão válidos.");
        return new()
        {
            Id = Guid.NewGuid(), EmpresaId = empresaId, ContaId = contaId, Operacao = "criar-aluguel",
            Chave = chave, HashComando = hashComando.ToUpperInvariant(), RevisaoId = revisaoId,
            VersaoOriginal = versaoOriginal.ToArray(), ConfirmadoEmUtc = agora.ToUniversalTime(),
        };
    }

    public static ComandoIdempotente RegistrarDevolucao(Guid empresaId, Guid contaId, Guid chave,
        string hashComando, Guid revisaoId, Guid demonstrativoFinalId, byte[] versaoOriginal, DateTimeOffset agora)
    {
        ValidacaoCadastro.Referencia(demonstrativoFinalId, "demonstrativoFinalId");
        var comando = RegistrarCriacao(empresaId, contaId, chave, hashComando, revisaoId, versaoOriginal, agora);
        comando.Operacao = "devolver-aluguel";
        comando.DemonstrativoFinalId = demonstrativoFinalId;
        return comando;
    }
}
