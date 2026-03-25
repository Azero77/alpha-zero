using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;

public class VideoUploadingDbContext : DbContext
{
    public VideoUploadingDbContext(DbContextOptions<VideoUploadingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("video_uploading");
    }
}
