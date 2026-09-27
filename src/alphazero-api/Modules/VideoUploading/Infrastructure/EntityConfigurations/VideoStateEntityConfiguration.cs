using AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.EntityConfigurations;

public class VideoStateEntityConfiguration : IEntityTypeConfiguration<VideoState>
{
    public void Configure(EntityTypeBuilder<VideoState> builder)
    {
        builder.HasKey(x => x.VideoId);
        builder.HasIndex(x => x.VideoId).IsUnique();
        builder.Property(x => x.Version)
            .IsConcurrencyToken();
        builder.Property(x => x.CustomThumbnailKey).HasMaxLength(512);
    }
}
