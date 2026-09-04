using CoreFinance.Domain.Common;
using CoreFinance.Domain.Entities;
using CoreFinance.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CoreFinance.Infra.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUser _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<FixedAccount> FixedAccounts => Set<FixedAccount>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Isolamento por usuário: nenhuma consulta enxerga dados de outro dono.
        modelBuilder.Entity<FixedAccount>().HasQueryFilter(x => x.UserId == _currentUser.Id);
        modelBuilder.Entity<Payment>().HasQueryFilter(x => x.UserId == _currentUser.Id);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        DefinirDonoDosNovosRegistros();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void DefinirDonoDosNovosRegistros()
    {
        var novos = ChangeTracker.Entries<OwnedEntity>()
            .Where(e => e.State == EntityState.Added && e.Entity.UserId == Guid.Empty);

        foreach (var entrada in novos)
        {
            entrada.Entity.DefinirDono(_currentUser.Id);
        }
    }
}
