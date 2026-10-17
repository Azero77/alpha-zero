using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Collections;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        // Table-name pin (#31): the DbSet was renamed Enrollements → Enrollments, which would change
        // the conventional table name. The database schema is intentionally NOT renamed (no migration).
        builder.ToTable("Enrollements", "Courses");

        builder.Property(e => e.Id)
            .ValueGeneratedNever();
        builder.Property(e => e.Status)
            .HasConversion<string>();
        builder.HasIndex(e => new { e.StudentId, e.CourseId }).IsUnique();

        builder.OwnsOne(b => b.Progress, p =>
        {
            p.Property(p => p.Bitmask)
                .HasColumnName("ProgressBitmask")
                .HasConversion(
                    v => v,
                    v => v)
                .HasColumnType("varbit");
            p.Property(p => p.TotalItems)
                .HasColumnName("ProgressTotalItems");
            p.Property(p => p.ActiveItems)
                .HasColumnName("ProgressActiveItems");
        });
    }
}
