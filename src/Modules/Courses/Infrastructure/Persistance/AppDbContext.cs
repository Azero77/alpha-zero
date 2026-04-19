using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using AlphaZero.Modules.Courses.Infrastructure.Sagas.CourseRedemption;
using AlphaZero.Shared.Infrastructure.Database;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance;

public class AppDbContext : DbContext,ITenantDbContext
{
    private readonly ITenantProvider _tenantProvider;
    public const string Schema = "Courses";

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Enrollement> Enrollements => Set<Enrollement>();
    public DbSet<CourseRedemptionState> CourseRedemptionStates => Set<CourseRedemptionState>();

    public Guid? TenantId => _tenantProvider.GetTenant();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        
        // Auto-apply Tenant and Soft Delete filters for all entities in this module
        modelBuilder.ApplyAlphaZeroGlobalFilters(this);

        base.OnModelCreating(modelBuilder);
    }
}
