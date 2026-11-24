using System.Threading.RateLimiting;
using Locadora.Api.Host;
using Locadora.Api.Modulos.Acesso.Sessao;
using Locadora.Application.Modulos.Transversal;
using Locadora.Infrastructure;
using Locadora.Infrastructure.Modulos.Acesso.Sessao;
using Locadora.Infrastructure.Persistencia;
using Locadora.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuracao = ValidadorConfiguracao.Carregar(builder.Configuration);

// Exceções SQL podem conter a chave duplicada mesmo sem sensitive-data logging.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddLocadora(configuracao);
builder.Services.AddOpenApi();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        opcoes.MapInboundClaims = false;
        opcoes.TokenValidationParameters = ParametrosToken.Criar(
            configuracao.Chaves,
            configuracao.Emissor,
            configuracao.Audiencia);
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(opcoes =>
{
    opcoes.AddPolicy("spa", politica =>
    {
        politica.WithOrigins(configuracao.Origem)
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete, HttpMethods.Options)
            .WithHeaders("Authorization", "Content-Type", "If-Match", "Idempotency-Key")
            .WithExposedHeaders("ETag")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});
builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opcoes.OnRejected = async (contexto, cancelamento) =>
    {
        contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        contexto.HttpContext.Response.ContentType = "application/problem+json";
        contexto.HttpContext.Response.Headers.CacheControl = "no-store";
        await contexto.HttpContext.Response.WriteAsync(
            ProblemaJson.Serializar(
                429,
                "Muitas requisições",
                "limite_excedido",
                contexto.HttpContext.TraceIdentifier,
                null),
            cancelamento);
    };
    opcoes.AddPolicy("registro", http => Particionar(http, configuracao.Limites.RegistroQuantidade, configuracao.Limites.RegistroJanela));
    opcoes.AddPolicy("login", http => Particionar(http, configuracao.Limites.LoginQuantidade, configuracao.Limites.LoginJanela));
});
builder.Services.AddScoped<FiltroSemCache>();
builder.Services.AddControllers(opcoes => opcoes.Filters.Add<FiltroErrosCadastro>())
    .AddJsonOptions(opcoes => opcoes.JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true)
    .ConfigureApiBehaviorOptions(opcoes => opcoes.InvalidModelStateResponseFactory = ValidacaoTransporte.Responder);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var escopo = app.Services.CreateAsyncScope();
    var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDbContext>();
    await banco.Database.MigrateAsync();
}

app.UseExceptionHandler(tratamento =>
{
    tratamento.Run(async contexto =>
    {
        var erro = contexto.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var indisponivel = erro is ArmazenamentoIndisponivelException || (erro is not null && FalhasArmazenamento.Eh(erro));
        var status = indisponivel ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        contexto.Response.StatusCode = status;
        contexto.Response.ContentType = "application/problem+json";
        contexto.Response.Headers.CacheControl = "no-store";
        await contexto.Response.WriteAsync(ProblemaJson.Serializar(
            status,
            indisponivel ? "Dependência indisponível" : "Erro interno",
            indisponivel ? "dependencia_indisponivel" : "erro_interno",
            contexto.TraceIdentifier,
            null));
    });
});

if (app.Environment.IsEnvironment("Testing"))
{
    app.Use(async (contexto, proximo) =>
    {
        if (contexto.Request.Headers.TryGetValue("X-Teste-Ip", out var valor)
            && System.Net.IPAddress.TryParse(valor.ToString(), out var ip))
        {
            contexto.Connection.RemoteIpAddress = ip;
        }

        await proximo();
    });
}

app.UseCors("spa");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ValidacaoSessaoMiddleware>();
app.MapControllers();
app.MapOpenApi();
app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.Run();

static RateLimitPartition<string> Particionar(HttpContext http, int quantidade, TimeSpan janela)
{
    var ip = http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
    return RateLimitPartition.GetFixedWindowLimiter(
        ip,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = quantidade,
            Window = janela,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
}

public partial class Program;
