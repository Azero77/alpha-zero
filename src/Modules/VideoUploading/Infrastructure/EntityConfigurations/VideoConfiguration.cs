using AlphaZero.Modules.VideoUploading.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.EntityConfigurations;

public class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.ToTable("Videos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever();
        
        builder.Property(x => x.Title).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.SourceKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.OutputFolder).HasMaxLength(512);

        builder.OwnsOne(x => x.Metadata, m =>
        {
            m.Property(p => p.OriginalFileName).HasColumnName("OriginalFileName").IsRequired();
            m.Property(p => p.ContentType).HasColumnName("ContentType").IsRequired();
            m.Property(p => p.FileSize).HasColumnName("FileSize").IsRequired();
            m.Property(p => p.TranscodingMethod).HasColumnName("Metadata_TranscodingMethod").IsRequired();
            m.Property(p => p.EncryptionMethod).HasColumnName("Metadata_EncryptionMethod");
        });

        builder.OwnsOne(x => x.Specifications, s =>
        {
            s.Property(p => p.Duration).HasColumnName("Duration");
            s.OwnsOne(p => p.Resolution, r =>
            {
                r.Property(p => p.width).HasColumnName("Width");
                r.Property(p => p.height).HasColumnName("Height");
            });
        });

        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.TenantId).IsRequired();
    }
}