using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class CourseSectionItemConfiguration : IEntityTypeConfiguration<CourseSectionItem>
{
    public void Configure(EntityTypeBuilder<CourseSectionItem> builder)
    {
        builder.Property(s => s.Id)
            .ValueGeneratedNever();
        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(i => i.Metadata)
            .HasColumnType("jsonb");

        builder.HasDiscriminator<string>("ItemType")
            .HasValue<CourseSectionLesson>("Lesson")
            .HasValue<CourseSectionAssessment>("Assessment")
            .HasValue<CourseSectionDocument>("Document");
    }
}
