using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;

public class AppDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;
    public const string Schema = "video_uploading";

    public DbSet<Video> Videos { get; set; } = null!;
    public DbSet<VideoState> VideoState { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<Video>().HasQueryFilter(x => x.TenantId == _tenantProvider.GetTenant());
    }

}
