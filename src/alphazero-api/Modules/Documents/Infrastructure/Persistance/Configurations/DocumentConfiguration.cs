using AlphaZero.Modules.Documents.Domain.Models;
using AlphaZero.Modules.Documents.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Documents.Infrastructure.Persistance.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents", AppDbContext.Schema);

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.Description)
            .HasMaxLength(2000);

        builder.Property(d => d.FileType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.S3Key)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.FileSizeBytes)
            .IsRequired();

        builder.Property(d => d.CreatedOn)
            .IsRequired();

        builder.Property(d => d.IsDeleted)
            .IsRequired();

        builder.Property(d => d.OnDeleted);

        builder.HasIndex(d => new { d.TenantId, d.IsDeleted });
    }
}
