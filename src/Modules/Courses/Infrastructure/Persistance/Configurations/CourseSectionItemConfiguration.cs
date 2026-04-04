using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class CourseSectionItemConfiguration : IEntityTypeConfiguration<CourseSectionItem>
{
    public void Configure(EntityTypeBuilder<CourseSectionItem> builder)
    {
        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasDiscriminator<string>("ItemType")
            .HasValue<CourseSectionLesson>("Lesson")
            .HasValue<CourseSectionQuiz>("Quiz")
            .HasValue<CourseSectionDocument>("Document");
    }
}
