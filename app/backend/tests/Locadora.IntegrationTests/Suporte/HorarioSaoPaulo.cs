namespace Locadora.IntegrationTests.Suporte;

/// <summary>Fuso operacional fixo; não depende do fuso do sistema onde os testes rodam.</summary>
internal static class HorarioSaoPaulo
{
    public static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTimeOffset Criar(int ano, int mes, int dia, int hora, int minuto, int segundo = 0)
    {
        var local = new DateTime(ano, mes, dia, hora, minuto, segundo, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Fuso.GetUtcOffset(local));
    }

    public static string ParaTransporte(DateTimeOffset instante)
    {
        var local = TimeZoneInfo.ConvertTime(instante, Fuso);
        return new DateTimeOffset(local.DateTime, Fuso.GetUtcOffset(local)).ToString("yyyy-MM-ddTHH:mm:sszzz");
    }
}