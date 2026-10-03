using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
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
               .IsRequired()
               .HasColumnType("jsonb");

        builder.Property(x => x.TargetAudienceContent)
               .HasColumnType("jsonb");

        builder.Property(x => x.LearningObjectivesContent)
               .HasColumnType("jsonb");

        builder.Property(x => x.ETag)
               .IsRequired()
               .HasMaxLength(64);
    }
}
