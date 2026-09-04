using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Shared.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Tenants.Infrastructure.Persistance;

public class AppDbContext : DbContext
{
    public const string Schema = "Tenants";

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
