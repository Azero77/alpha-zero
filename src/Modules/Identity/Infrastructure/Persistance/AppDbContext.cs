using System.Reflection;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
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

    public DbSet<PrincipalDataModel> Principals => Set<PrincipalDataModel>();
    public DbSet<ManagedPolicy> ManagedPolicies => Set<ManagedPolicy>();
    public DbSet<PrincipalPolicyAssignment> PrincipalPolicyAssignments => Set<PrincipalPolicyAssignment>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<TenantUserPrinciaplAssignment> TenantPrinciaplAssignments => Set<TenantUserPrinciaplAssignment>();
    public DbSet<ConditionDefinition> ConditionDefinitions => Set<ConditionDefinition>();

    public Guid? TenantId => tenantProvider.GetTenant();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.ApplyAlphaZeroGlobalFilters(this);

        // Ignore JSON-only types and Domain types that have separate DataModels
        modelBuilder.Ignore<PolicyStatement>();
        modelBuilder.Ignore<ManagedPolicyStatement>();
        modelBuilder.Ignore<Principal>(); 

        base.OnModelCreating(modelBuilder);
    }
}
