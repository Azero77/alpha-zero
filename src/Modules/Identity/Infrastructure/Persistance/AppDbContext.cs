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
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<ManagedPolicy> ManagedPolicies => Set<ManagedPolicy>();
    public DbSet<PrincipalPolicyAssignment> PrincipalPolicyAssignments => Set<PrincipalPolicyAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
