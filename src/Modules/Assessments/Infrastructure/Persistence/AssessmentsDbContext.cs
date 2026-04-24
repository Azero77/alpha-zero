using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions;
using AlphaZero.Shared.Infrastructure.Database;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Assessments.Infrastructure.Persistence;

public class AssessmentsDbContext : DbContext, ITenantDbContext
{
    private readonly ITenantProvider _tenantProvider;
    public const string Schema = "Assessments";

    public AssessmentsDbContext(DbContextOptions<AssessmentsDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentSubmission> Submissions => Set<AssessmentSubmission>();

    public Guid? TenantId => _tenantProvider.GetTenant();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssessmentsDbContext).Assembly);
        
        modelBuilder.ApplyAlphaZeroGlobalFilters(this);

        base.OnModelCreating(modelBuilder);
    }
}
