using System.Reflection;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Identity.Infrastructure.Persistance;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Principal> Principals => Set<Principal>();
    public DbSet<PrincipalTemplate> PrincipalTemplates => Set<PrincipalTemplate>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<ManagedPolicy> ManagedPolicies => Set<ManagedPolicy>();
    public DbSet<PrincipalPolicyAssignment> PrincipalPolicyAssignments => Set<PrincipalPolicyAssignment>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<TenantPrinciaplAssignment> TenantPrinciaplAssignments => Set<TenantPrinciaplAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
