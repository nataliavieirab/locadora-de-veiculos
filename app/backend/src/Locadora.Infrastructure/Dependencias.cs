using Locadora.Application.Modulos.Acesso.Registro;
using Locadora.Application.Modulos.Acesso.Sessao;
using Locadora.Domain.Modulos.Acesso;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.Acesso.Sessao;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Locadora.Infrastructure;

public static class Dependencias
{
    public static IServiceCollection AddLocadora(this IServiceCollection services, ConfiguracaoAplicacao configuracao)
    {
        services.AddSingleton(configuracao);
        services.AddSingleton(configuracao.Chaves);
        services.AddSingleton(configuracao.Limites);
        services.AddScoped<ContextoEmpresa>();
        services.AddScoped<GuardaEscritaEmpresa>();
        services.AddDbContext<LocadoraDbContext>((provedor, opcoes) =>
        {
            opcoes.UseSqlServer(configuracao.Conexao);
            opcoes.AddInterceptors(provedor.GetRequiredService<GuardaEscritaEmpresa>());
        });

        services.AddIdentityCore<UsuarioConta>(opcoes =>
            {
                opcoes.Password.RequiredLength = ValidadorRegistro.SenhaMinima;
                opcoes.Password.RequiredUniqueChars = 0;
                opcoes.Password.RequireDigit = false;
                opcoes.Password.RequireLowercase = false;
                opcoes.Password.RequireUppercase = false;
                opcoes.Password.RequireNonAlphanumeric = false;
                opcoes.Lockout.MaxFailedAccessAttempts = 5;
                opcoes.Lockout.DefaultLockoutTimeSpan = PrazosSessao.BloqueioConta;
                opcoes.Lockout.AllowedForNewUsers = true;
                opcoes.User.RequireUniqueEmail = false;
                // Lista vazia desliga o conjunto padrão do Identity e aceita qualquer caractere.
                // Espaço, tamanho e formato continuam no validador da aplicação.
                opcoes.User.AllowedUserNameCharacters = string.Empty;
            })
            .AddEntityFrameworkStores<LocadoraDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<ServicoRegistroEmpresa>();
        services.AddScoped<IConsultaUsername>(provedor => provedor.GetRequiredService<ServicoRegistroEmpresa>());
        services.AddScoped<IRegistroEmpresa>(provedor => provedor.GetRequiredService<ServicoRegistroEmpresa>());
        services.AddScoped<CasoRegistroEmpresa>();
        services.AddScoped<IServicoSessao, ServicoSessao>();
        services.AddSingleton(provedor => new EmissorJwt(
            provedor.GetRequiredService<MapaChavesJwt>(),
            configuracao.Emissor,
            configuracao.Audiencia,
            provedor.GetRequiredService<TimeProvider>()));

        if (services.All(servico => servico.ServiceType != typeof(IGanchoRegistro)))
        {
            services.AddSingleton<IGanchoRegistro, GanchoRegistroNulo>();
        }

        if (services.All(servico => servico.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }

        return services;
    }
}
