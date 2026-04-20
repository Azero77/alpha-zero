using System.Reflection;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Shared.Infrastructure.Database;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Identity.Infrastructure.Persistance;

public class AppDbContext : DbContext, ITenantDbContext
{
    private readonly ITenantProvider tenantProvider;
    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        this.tenantProvider = tenantProvider;
    }

    public DbSet<Principal> Principals => Set<Principal>();
    public DbSet<PrincipalTemplate> PrincipalTemplates => Set<PrincipalTemplate>();
    public DbSet<ManagedPolicy> ManagedPolicies => Set<ManagedPolicy>();
    public DbSet<PrincipalPolicyAssignment> PrincipalPolicyAssignments => Set<PrincipalPolicyAssignment>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<TenantUserPrinciaplAssignment> TenantPrinciaplAssignments => Set<TenantUserPrinciaplAssignment>();

    public Guid? TenantId => tenantProvider.GetTenant();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.ApplyAlphaZeroGlobalFilters(this);

        // Ignore JSON-only types to prevent EF from treating them as entities
        modelBuilder.Ignore<Policy>();
        modelBuilder.Ignore<PolicyStatement>();
        modelBuilder.Ignore<PolicyTemplateStatement>();

        // --- Data Seeding ---
        var (principals, managedPolicies, assignments) = Seeding.IdentitySeedReader.GetData();

        modelBuilder.Entity<ManagedPolicy>().HasData(managedPolicies);
        modelBuilder.Entity<PrincipalTemplate>().HasData(principals);
        modelBuilder.Entity<PrincipalPolicyAssignment>().HasData(assignments);
        
        base.OnModelCreating(modelBuilder);
    }
}
