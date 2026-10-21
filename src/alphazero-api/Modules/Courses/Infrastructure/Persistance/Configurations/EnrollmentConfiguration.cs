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

        // Optimistic concurrency (#32/D3): the Npgsql xmin system column backs a uint
        // rowversion token. A concurrent completion's stale save fails with
        // DbUpdateConcurrencyException; the completion write path retries ONCE against
        // fresh loads, and the aggregate's CompletedOn guard then suppresses the
        // duplicate completion milestone. (xmin is a system column — no physical DDL.)
        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.OwnsOne(b => b.Progress, p =>
        {
            // Only the student's completion bits are stored. TotalItems/ActiveItems used to be
            // persisted here; they are derived from the course's ActiveItemsBitmask at
            // calculation time (ProgressCalculation) — never stored per enrollment.
            p.Property(p => p.Bitmask)
                .HasColumnName("ProgressBitmask")
                .HasConversion(
                    v => v,
                    v => v)
                .HasColumnType("varbit");
        });
    }
}
