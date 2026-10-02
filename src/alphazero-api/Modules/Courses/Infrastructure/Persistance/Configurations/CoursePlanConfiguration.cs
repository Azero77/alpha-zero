using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class CoursePlanConfiguration : IEntityTypeConfiguration<CoursePlan>
{
    public void Configure(EntityTypeBuilder<CoursePlan> builder)
    {
        builder.ToTable("CoursePlans");

        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(p => p.PrincipalId)
            .IsRequired();

        // Ensure plan names are unique per course
        builder.HasIndex(p => new { p.CourseId, p.Name }).IsUnique();
    }
}
