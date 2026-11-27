namespace Locadora.Application.Modulos.Fotos;

public sealed record ImagemSaneada(byte[] Bytes, string Mime, int Largura, int Altura);

public enum FalhaProcessamentoImagem { Invalida, Formato, Excesso, Indisponivel }

public sealed class ProcessamentoImagemException(FalhaProcessamentoImagem falha)
    : Exception("Não foi possível processar a imagem.")
{
    public FalhaProcessamentoImagem Falha { get; } = falha;
}

public interface IProcessadorImagem
{
    Task<ImagemSaneada> ProcessarAsync(Stream arquivo, string mime, CancellationToken cancelamento);
}
