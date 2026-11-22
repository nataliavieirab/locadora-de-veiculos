using Locadora.Infrastructure.Transversal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Locadora.Infrastructure.Persistencia;

public sealed class FabricaDesign : IDesignTimeDbContextFactory<LocadoraDbContext>
{
    public LocadoraDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("LOCADORA_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(conexao))
        {
            throw new InvalidOperationException("Defina LOCADORA_SQL_CONNECTION para criar migrations.");
        }

        var opcoes = new DbContextOptionsBuilder<LocadoraDbContext>()
            .UseSqlServer(conexao)
            .Options;
        return new LocadoraDbContext(opcoes, new ContextoEmpresa());
    }
}
