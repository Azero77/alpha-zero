using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class CourseSectionConfiguration : IEntityTypeConfiguration<CourseSection>
{
    public void Configure(EntityTypeBuilder<CourseSection> builder)
    {
        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
