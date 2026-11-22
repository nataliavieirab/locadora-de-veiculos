using System.Text.Json;

namespace Locadora.Api.Host;

public static class ProblemaJson
{
    public static string Serializar(
        int status,
        string title,
        string code,
        string traceId,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        var corpo = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["title"] = title,
            ["code"] = code,
            ["traceId"] = traceId,
        };
        if (errors is not null)
        {
            corpo["errors"] = errors;
        }

        return JsonSerializer.Serialize(corpo);
    }
}
