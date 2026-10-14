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

        builder.Ignore(r => r.ResourceType);

        builder.Property<string>("Discriminator")
            .HasColumnName("ResourceType");

        builder.HasDiscriminator<string>("Discriminator")
            .HasValue<VideoCurriculumResource>(nameof(ResourceType.Video))
            .HasValue<DocumentCurriculumResource>(nameof(ResourceType.Document))
            .HasValue<ArticleCurriculumResource>(nameof(ResourceType.Article))
            .HasValue<AssessmentCurriculumResource>(nameof(ResourceType.Assessment));

        builder.Property(r => r.Order)
            .IsRequired();

        builder.Property(r => r.CourseAssetId)
            .IsRequired(false);

        builder.HasOne<CourseAsset>()
            .WithMany()
            .HasForeignKey(r => r.CourseAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.CourseAssetId);
        builder.HasIndex(r => r.CurriculumItemId);
    }
}

public class VideoCurriculumResourceConfiguration : IEntityTypeConfiguration<VideoCurriculumResource>
{
    public void Configure(EntityTypeBuilder<VideoCurriculumResource> builder)
    {
        builder.Ignore(v => v.VideoId);
        builder.Property(v => v.DurationSeconds);
    }
}

public class DocumentCurriculumResourceConfiguration : IEntityTypeConfiguration<DocumentCurriculumResource>
{
    public void Configure(EntityTypeBuilder<DocumentCurriculumResource> builder)
    {
        builder.Ignore(d => d.DocumentId);
        builder.Ignore(d => d.DocUri);
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
        builder.Ignore(a => a.TextContent);
        builder.Property(a => a.Content);
    }
}

public class QuizCurriculumResourceConfiguration : IEntityTypeConfiguration<AssessmentCurriculumResource>
{
    public void Configure(EntityTypeBuilder<AssessmentCurriculumResource> builder)
    {
        builder.Ignore(q => q.AssessmentId);
        builder.Property(q => q.PassingScore);
    }
}
