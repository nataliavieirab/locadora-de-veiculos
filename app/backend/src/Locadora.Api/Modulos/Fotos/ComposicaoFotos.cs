using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Amazon.S3;
using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Fotos;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;

namespace Locadora.Api.Modulos.Fotos;

public static class ComposicaoFotos
{
    public static IServiceCollection AddFotosPrivadas(this IServiceCollection services,
        IConfiguration configuracao, IWebHostEnvironment ambiente)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IRepositorioFotos, RepositorioFotos>();
        services.AddScoped<IReferenciaFotoAtual, ReferenciaFotoAtual>();
        services.AddScoped<ISessaoFotos>(provedor =>
        {
            var usuario = provedor.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;
            if (!Guid.TryParse(usuario?.FindFirstValue(JwtRegisteredClaimNames.Sid), out var sid)
                || !int.TryParse(usuario?.FindFirstValue("versaoAcesso"), out var versao))
                throw new SessaoFotoInvalidaException();
            return new SessaoFotos(provedor.GetRequiredService<LocadoraDbContext>(),
                provedor.GetRequiredService<ContextoEmpresa>(), sid, versao,
                provedor.GetRequiredService<TimeProvider>());
        });
        services.AddScoped<CasosFotos>();
        services.AddScoped<ConsultaFotos>();
        var fotos = configuracao.GetSection("Fotos");
        var provedor = fotos["Provedor"];
        if (provedor == "Local")
        {
            services.AddSingleton(new DestinoFotos(ProvedorFoto.Local, "local-privado"));
            services.AddSingleton<IArmazenamentoObjeto>(_ => new ArmazenamentoAdiado(() => OperatingSystem.IsLinux()
                ? new ArmazenamentoLocal(fotos["RaizPrivada"] ?? "", ambiente.WebRootPath
                    ?? Path.Combine(ambiente.ContentRootPath, "wwwroot"))
                : new FotosIndisponiveis()));
        }
        else if (provedor == "R2")
        {
            services.AddSingleton(new DestinoFotos(ProvedorFoto.R2, fotos["R2:Bucket"] ?? ""));
            services.AddSingleton(_ => new ConfiguracaoR2
            {
                Endpoint = fotos["R2:Endpoint"] ?? "", Bucket = fotos["R2:Bucket"] ?? "",
                AccessKey = fotos["R2:AccessKey"] ?? "", SecretKey = fotos["R2:SecretKey"] ?? "",
                BucketPrivadoConfirmado = fotos.GetValue<bool>("R2:BucketPrivadoConfirmado"),
            });
            services.AddSingleton<IAmazonS3>(p => p.GetRequiredService<ConfiguracaoR2>().CriarCliente());
            services.AddSingleton<IArmazenamentoObjeto>(p => new ArmazenamentoAdiado(() =>
                new ArmazenamentoR2(p.GetRequiredService<IAmazonS3>(), p.GetRequiredService<ConfiguracaoR2>())));
        }
        else
        {
            // Não habilitar storage implicitamente nem impedir o cadastro sem foto.
            services.AddSingleton(new DestinoFotos(ProvedorFoto.Local, "desabilitado"));
            services.AddSingleton<IArmazenamentoObjeto, FotosIndisponiveis>();
        }
        services.AddSingleton<IProcessadorImagem>(_ =>
            new ProcessadorImagemIsolado(new(fotos["Processador:Executavel"] ?? "",
                fotos["Processador:Assembly"] ?? "", fotos["Processador:Cgroups"] ?? "",
                fotos["Processador:Temporarios"] ?? "")));
        return services;
    }

    // Configuração/volume indisponíveis não podem impedir consulta cadastral.
    private sealed class ArmazenamentoAdiado(Func<IArmazenamentoObjeto> criar) : IArmazenamentoObjeto, IDisposable
    {
        private readonly Lazy<IArmazenamentoObjeto> objeto = new(criar);
        public Task GravarAsync(ObjetoFoto foto, ReadOnlyMemory<byte> bytes, CancellationToken token) => objeto.Value.GravarAsync(foto, bytes, token);
        public Task<byte[]> LerAsync(ObjetoFoto foto, CancellationToken token) => objeto.Value.LerAsync(foto, token);
        public Task RemoverAsync(ObjetoFoto foto, CancellationToken token) => objeto.Value.RemoverAsync(foto, token);
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto foto, CancellationToken token) => objeto.Value.ConfirmarAusenciaAsync(foto, token);
        public void Dispose() { if (objeto.IsValueCreated && objeto.Value is IDisposable recurso) recurso.Dispose(); }
    }

    private sealed class FotosIndisponiveis : IArmazenamentoObjeto
    {
        public Task GravarAsync(ObjetoFoto objeto, ReadOnlyMemory<byte> bytes, CancellationToken cancelamento) =>
            throw new ArmazenamentoObjetoException();
        public Task<byte[]> LerAsync(ObjetoFoto objeto, CancellationToken cancelamento) =>
            throw new ArmazenamentoObjetoException();
        public Task RemoverAsync(ObjetoFoto objeto, CancellationToken cancelamento) =>
            throw new ArmazenamentoObjetoException();
        public Task<bool> ConfirmarAusenciaAsync(ObjetoFoto objeto, CancellationToken cancelamento) =>
            throw new ArmazenamentoObjetoException();
    }
}
