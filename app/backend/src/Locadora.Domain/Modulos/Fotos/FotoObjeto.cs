using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Fotos;

public enum EstadoFoto { Pendente, Ativa, Inativa, Abandonada }
public enum ProvedorFoto { Local, R2 }

public sealed class FotoObjeto : EntidadeCadastro
{
    public const int MaximoBytes = 5 * 1024 * 1024;
    public const long MaximoPixels = 16_000_000;
    public Guid VeiculoId { get; private set; }
    public EstadoFoto Estado { get; private set; }
    public string Mime { get; private set; } = "";
    public int TamanhoBytes { get; private set; }
    public int Largura { get; private set; }
    public int Altura { get; private set; }
    public byte[] Sha256 { get; private set; } = [];
    public ProvedorFoto Provedor { get; private set; }
    public string Bucket { get; private set; } = "";
    public string Chave { get; private set; } = "";
    public string Caminho { get; private set; } = "";
    public byte[] VersaoBase { get; private set; } = [];
    public Guid AtorId { get; private set; }
    public string Correlacao { get; private set; } = "";
    public DateTimeOffset? RemovidaEmUtc { get; private set; }
    public DateTimeOffset? ExcluirAposUtc { get; private set; }
    public bool RetencaoLegada { get; private set; }

    public bool ElegivelParaRemocao(DateTimeOffset agora) =>
        Estado == EstadoFoto.Inativa && ExcluirAposUtc is { } piso && agora >= piso;

    private FotoObjeto() { }
    private FotoObjeto(Guid empresaId, DateTimeOffset agora) : base(empresaId, agora) { }

    // Bucket/provedor são configuração interna; não há parâmetro para URL, chave ou nome externo.
    public static FotoObjeto Preparar(Veiculo veiculo, string mime, int tamanhoBytes, int largura, int altura,
        byte[] sha256, byte[] versaoBase, ProvedorFoto provedor, string bucket, Guid atorId,
        string correlacao, DateTimeOffset agora)
    {
        if (veiculo.ExcluidoEmUtc is not null || agora < veiculo.AlteradoEmUtc)
            throw new InvalidOperationException("O veículo não permite preparar a foto.");
        if (mime is not ("image/jpeg" or "image/png") || tamanhoBytes is <= 0 or > MaximoBytes
            || largura <= 0 || altura <= 0 || (long)largura * altura > MaximoPixels
            || sha256.Length != 32 || versaoBase.Length != 8 || !Enum.IsDefined(provedor)
            || atorId == Guid.Empty)
            throw new ValidacaoCadastroException("arquivo", "Metadados da imagem inválidos.");
        var destino = ValidacaoCadastro.Texto(bucket, "bucket", 63);
        if (destino.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '.'))
            || destino.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Destino de armazenamento inválido.", nameof(bucket));
        var foto = new FotoObjeto(veiculo.EmpresaId, agora)
        {
            VeiculoId = veiculo.Id, Mime = mime, TamanhoBytes = tamanhoBytes, Largura = largura, Altura = altura,
            Sha256 = sha256.ToArray(), VersaoBase = versaoBase.ToArray(), Provedor = provedor,
            Bucket = destino, AtorId = atorId, Correlacao = ValidacaoCadastro.Texto(correlacao, "correlacao", 64)
        };
        foto.Chave = $"{veiculo.EmpresaId:N}/{foto.Id:N}";
        foto.Caminho = $"/api/v1/veiculos/{veiculo.Id:D}/foto";
        return foto;
    }

    internal void Ativar(DateTimeOffset agora)
    {
        if (Estado != EstadoFoto.Pendente)
            throw new InvalidOperationException("Somente uma pendência pode ser ativada.");
        RegistrarAlteracao(agora);
        Estado = EstadoFoto.Ativa;
    }

    internal void Inativar(DateTimeOffset agora)
    {
        if (Estado != EstadoFoto.Ativa)
            throw new InvalidOperationException("Somente a foto ativa pode ser inativada.");
        RegistrarAlteracao(agora);
        Estado = EstadoFoto.Inativa;
        RemovidaEmUtc = agora.ToUniversalTime();
        ExcluirAposUtc = RemovidaEmUtc.Value.AddDays(30);
    }

    public void Abandonar(DateTimeOffset agora)
    {
        if (Estado != EstadoFoto.Pendente)
            throw new InvalidOperationException("Uma foto anteriormente ativa não pode ser compensada.");
        RegistrarAlteracao(agora);
        Estado = EstadoFoto.Abandonada;
    }
}
