using AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.EntityConfigurations;

public class VideoStateEntityConfiguration : IEntityTypeConfiguration<VideoState>
{
    public void Configure(EntityTypeBuilder<VideoState> builder)
    {
        builder.Property(x => x.Version)
            .IsConcurrencyToken();
    }
}
