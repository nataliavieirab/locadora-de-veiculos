using Microsoft.AspNetCore.Mvc.Filters;

namespace Locadora.Api.Host;

public sealed class FiltroSemCache : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext contexto, ResultExecutionDelegate proximo)
    {
        if (!contexto.HttpContext.Response.Headers.ContainsKey("Cache-Control"))
            contexto.HttpContext.Response.Headers.CacheControl = "no-store";
        await proximo();
    }
}
