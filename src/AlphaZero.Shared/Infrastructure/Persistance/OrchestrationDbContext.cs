using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace AlphaZero.Shared.Infrastructure.Persistance;

public class OrchestrationDbContext : DbContext
{
    public const string Schema = "Orchestration";
    public OrchestrationDbContext(DbContextOptions<OrchestrationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
    }
}
