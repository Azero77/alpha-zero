using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class CurriculumResourceConfiguration : IEntityTypeConfiguration<CurriculumResource>
{
    public void Configure(EntityTypeBuilder<CurriculumResource> builder)
    {
        builder.ToTable("CurriculumResources", "Courses");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.HasDiscriminator(r => r.ResourceType)
            .HasValue<VideoCurriculumResource>("Video")
            .HasValue<DocumentCurriculumResource>("Document")
            .HasValue<ArticleCurriculumResource>("Article")
            .HasValue<AssessmentCurriculumResource>("Quiz");

        builder.Property(r => r.Order)
            .IsRequired();

        builder.Property(r => r.CourseAssetId)
            .IsRequired(false);

        builder.HasOne(r => r.Asset)
            .WithMany()
            .HasForeignKey(r => r.CourseAssetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.CourseAssetId);
        builder.HasIndex(r => r.CurriculumItemId);
    }
}

public class VideoCurriculumResourceConfiguration : IEntityTypeConfiguration<VideoCurriculumResource>
{
    public void Configure(EntityTypeBuilder<VideoCurriculumResource> builder)
    {
        builder.Property(v => v.VideoId);
        builder.Property(v => v.DurationSeconds);
    }
}

public class DocumentCurriculumResourceConfiguration : IEntityTypeConfiguration<DocumentCurriculumResource>
{
    public void Configure(EntityTypeBuilder<DocumentCurriculumResource> builder)
    {
        builder.Property(d => d.DocumentId);
        builder.Property(d => d.Title).HasMaxLength(256);
        builder.Property(d => d.DocumentType).HasMaxLength(50);
        builder.Property(d => d.IsPublic);
        builder.Property(d => d.VariantName).HasMaxLength(100);
    }
}

public class ArticleCurriculumResourceConfiguration : IEntityTypeConfiguration<ArticleCurriculumResource>
{
    public void Configure(EntityTypeBuilder<ArticleCurriculumResource> builder)
    {
        builder.Property(a => a.TextContent);
    }
}

public class QuizCurriculumResourceConfiguration : IEntityTypeConfiguration<AssessmentCurriculumResource>
{
    public void Configure(EntityTypeBuilder<AssessmentCurriculumResource> builder)
    {
        builder.Property(q => q.AssessmentId);
        builder.Property(q => q.PassingScore);
    }
}
