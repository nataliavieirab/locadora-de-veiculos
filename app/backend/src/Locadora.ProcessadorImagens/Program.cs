using Locadora.ProcessadorImagens;
using Locadora.ProtocoloFotos;

// Nenhum Host/configuração/SDK de SQL ou storage é carregado neste processo.
using var entrada = Console.OpenStandardInput();
using var saida = Console.OpenStandardOutput();
try
{
    saida.Write(ProtocoloProcessador.Pronto);
    saida.Flush();
    if (entrada.ReadByte() != ProtocoloProcessador.Iniciar || !Isolado())
        return 2;
    var mime = entrada.ReadByte() switch { 1 => "image/jpeg", 2 => "image/png", _ => "" };
    var tamanho = await ProtocoloProcessador.LerInteiroAsync(entrada, CancellationToken.None);
    if (tamanho is <= 0 or > ProtocoloProcessador.MaximoBytes)
    {
        saida.WriteByte(tamanho > ProtocoloProcessador.MaximoBytes ? ProtocoloProcessador.Excesso : ProtocoloProcessador.Invalida);
        return 0;
    }
    var bytes = new byte[tamanho];
    await entrada.ReadExactlyAsync(bytes);
    var resultado = SanitizadorImagem.Sanear(bytes, mime);
    saida.WriteByte(ProtocoloProcessador.Sucesso);
    saida.WriteByte(resultado.Mime == "image/png" ? (byte)2 : (byte)1);
    ProtocoloProcessador.EscreverInteiro(saida, resultado.Largura);
    ProtocoloProcessador.EscreverInteiro(saida, resultado.Altura);
    ProtocoloProcessador.EscreverInteiro(saida, resultado.Bytes.Length);
    saida.Write(resultado.Bytes);
    return 0;
}
catch (ImagemRejeitadaException erro)
{
    saida.WriteByte(erro.Codigo);
    return 0;
}
catch
{
    // Falha técnica é sinalizada por exit code, nunca exceção crua/bytes em log.
    return 2;
}

static bool Isolado()
{
    if (!OperatingSystem.IsLinux()) return false;
    var linha = File.ReadAllLines("/proc/self/cgroup").Single(l => l.StartsWith("0::", StringComparison.Ordinal));
    var grupo = Path.Combine("/sys/fs/cgroup", linha[3..].TrimStart('/'));
    return File.ReadAllText(Path.Combine(grupo, "memory.max")).Trim() == "268435456"
        && File.ReadAllText(Path.Combine(grupo, "memory.swap.max")).Trim() == "0";
}
