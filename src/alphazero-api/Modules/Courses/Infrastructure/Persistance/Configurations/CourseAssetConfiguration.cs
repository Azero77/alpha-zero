using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class CourseAssetConfiguration : IEntityTypeConfiguration<CourseAsset>
{
    public void Configure(EntityTypeBuilder<CourseAsset> builder)
    {
        builder.ToTable("CourseAssets", "Courses");

        // TPH discriminator
        builder.HasDiscriminator<string>("AssetType")
            .HasValue<VideoCourseAsset>("Video")
            .HasValue<DocumentCourseAsset>("Document")
            .HasValue<AssessmentCourseAsset>("Assessment");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.ResourceArn)
            .HasConversion(
                arn => arn.Value,
                val => ResourceArn.Create(val).Value)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Title).IsRequired().HasMaxLength(256);
        builder.Property(a => a.CourseId).IsRequired();
        builder.Property(a => a.State).IsRequired()
            .HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.UploadedUtcAt).IsRequired();

        builder.HasIndex(a => new { a.CourseId, a.State });
        builder.HasIndex(a => a.ResourceArn).IsUnique();
    }
}

public class VideoCourseAssetConfiguration : IEntityTypeConfiguration<VideoCourseAsset>
{
    public void Configure(EntityTypeBuilder<VideoCourseAsset> builder)
    {
        builder.Property(v => v.Duration);
        builder.Property(v => v.ThumbnailUrl).HasMaxLength(500);
        builder.Property(v => v.RelativeStreamingUrl).HasMaxLength(500);
    }
}

public class DocumentCourseAssetConfiguration : IEntityTypeConfiguration<DocumentCourseAsset>
{
    public void Configure(EntityTypeBuilder<DocumentCourseAsset> builder)
    {
        builder.Property(d => d.FileName).HasMaxLength(256);
        builder.Property(d => d.Size);
        builder.Property(d => d.ContentType).HasMaxLength(100);
    }
}

public class AssessmentCourseAssetConfiguration : IEntityTypeConfiguration<AssessmentCourseAsset>
{
    public void Configure(EntityTypeBuilder<AssessmentCourseAsset> builder)
    {
        builder.Property(a => a.QuestionsNumber);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
    }
}
