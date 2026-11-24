using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public static class TempoLocacao
{
    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static int ContarDiarias(DateTimeOffset saida, DateTimeOffset retorno)
    {
        var duracao = retorno.UtcTicks - saida.UtcTicks;
        if (duracao < 0)
        {
            throw new ValidacaoCadastroException("retornoPrevisto", "O retorno não pode preceder a saída.");
        }

        // Divisão inteira evita perder um tick no limite da diária.
        return checked((int)Math.Max(1, duracao / TimeSpan.TicksPerDay
            + (duracao % TimeSpan.TicksPerDay == 0 ? 0 : 1)));
    }

    public static DateOnly DiaSaoPaulo(DateTimeOffset instante)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, Fuso).DateTime);

    public static DateTimeOffset NormalizarHorarioSaoPaulo(DateTimeOffset horario)
    {
        var local = DateTime.SpecifyKind(horario.DateTime, DateTimeKind.Unspecified);
        // O offset explícito desambigua a hora repetida, mas deve ser um dos offsets reais.
        if (Fuso.IsInvalidTime(local)
            || TimeZoneInfo.ConvertTime(horario, Fuso).DateTime != local
            || Fuso.GetUtcOffset(horario) != horario.Offset)
        {
            throw new ValidacaoCadastroException("retornoPrevisto", "Informe um horário existente com o offset de São Paulo.");
        }

        return horario.ToUniversalTime();
    }
}
