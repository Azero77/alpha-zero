using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Infrastructure.Database;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Library.Infrastructure.Persistance;

public class AppDbContext : DbContext, ITenantDbContext
{
    private readonly ITenantProvider _tenantProvider;
    public const string Schema = "Library";

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<AccessCode> AccessCodes => Set<AccessCode>();

    public Guid? TenantId => _tenantProvider.GetTenant();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        
        modelBuilder.ApplyAlphaZeroGlobalFilters(this);

        base.OnModelCreating(modelBuilder);
    }
}
