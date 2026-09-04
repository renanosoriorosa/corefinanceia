using CoreFinance.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CoreFinance.Infra.Data;

/// <summary>
/// Usado apenas pelas ferramentas de linha de comando do EF Core (migrations),
/// onde não existe requisição HTTP nem usuário autenticado.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "CoreFinance.API"))
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
            .Options;

        return new AppDbContext(options, new UsuarioDeDesignTime());
    }

    private class UsuarioDeDesignTime : ICurrentUser
    {
        public Guid Id => Guid.Empty;
        public bool Autenticado => false;
    }
}
