using AlphaZero.Modules.VideoUploading.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.EntityConfigurations;

public class VideoSecretConfiguration : IEntityTypeConfiguration<VideoSecret>
{
    public void Configure(EntityTypeBuilder<VideoSecret> builder)
    {
        builder.ToTable("VideoSecrets");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.VideoId).IsRequired();
        builder.HasIndex(x => x.VideoId).IsUnique();

        builder.Property(x => x.KeyId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.KeyValue).HasMaxLength(512).IsRequired();
        builder.Property(x => x.IV).HasMaxLength(128);
    }
}
