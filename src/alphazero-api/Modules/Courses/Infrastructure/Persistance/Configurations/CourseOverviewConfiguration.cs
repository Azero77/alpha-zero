using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

internal class CourseOverviewConfiguration : IEntityTypeConfiguration<CourseOverview>
{
    public void Configure(EntityTypeBuilder<CourseOverview> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.CourseId).IsUnique();
        
        builder.HasOne<Course>()
               .WithOne()
               .HasForeignKey<CourseOverview>(x => x.CourseId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.DescriptionContent)
               .HasConversion(
                   v => v.Value,
                   v => RichText.Create(v).Value)
               .IsRequired()
               .HasColumnType("jsonb");

        builder.Property(x => x.TargetAudienceContent)
               .HasConversion(
                   v => v != null ? v.Value : null,
                   v => v != null ? RichText.Create(v).Value : null)
               .HasColumnType("jsonb");

        builder.Property(x => x.LearningObjectivesContent)
               .HasConversion(
                   v => v != null ? v.Value : null,
                   v => v != null ? RichText.Create(v).Value : null)
               .HasColumnType("jsonb");

        builder.Property(x => x.ETag)
               .IsRequired()
               .HasMaxLength(64);
    }
}
