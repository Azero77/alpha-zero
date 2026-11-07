using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Models;
using AlphaZero.Shared.Infrastructure.Database;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;

using MassTransit;

namespace AlphaZero.Modules.Library.Infrastructure.Persistance;

public class AppDbContext : DbContext, ITenantDbContext
{
    private readonly ITenantProvider _tenantProvider;
    public const string Schema = "Library";

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<AccessCodeDataModel> AccessCodes => Set<AccessCodeDataModel>();
    public DbSet<Domain.Library> Libraries => Set<Domain.Library>();
    public DbSet<RedemptionAuditLog> RedemptionAuditLogs => Set<RedemptionAuditLog>();
    public DbSet<SaleRecord> SaleRecords => Set<SaleRecord>();
    public DbSet<LibraryAuditLog> LibraryAuditLogs => Set<LibraryAuditLog>();
    public DbSet<Models.LibraryAnalytics> LibraryAnalytics => Set<Models.LibraryAnalytics>();

    public Guid? TenantId => _tenantProvider.GetTenant();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        
        modelBuilder.ApplyAlphaZeroGlobalFilters(this);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}
