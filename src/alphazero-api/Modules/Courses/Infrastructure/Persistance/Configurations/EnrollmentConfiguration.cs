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

        // Complex property, NOT OwnsOne (#32/D3): an owned type is modelled as a separate
        // entity type with its own key and owner FK, so FromSql composition fetched it through a
        // self-join of the same table. That join alias was scanned with the pre-lock snapshot,
        // so a completion that waited on another writer's FOR UPDATE still materialised the old
        // bitmask and overwrote it — see GetByIdForUpdateAsync. A complex type is part of the
        // owner's row, so the lock and the returned mask come from the same fetch.
        //
        // Only the student's completion bits are stored. TotalItems/ActiveItems used to be
        // persisted here; they are derived from the course's ActiveItemsBitmask at
        // calculation time (ProgressCalculation) — never stored per enrollment.
        builder.ComplexProperty(b => b.Progress, p =>
        {
            p.Property(x => x.Bitmask)
                .HasColumnName("ProgressBitmask")
                .HasConversion(
                    v => v,
                    v => v)
                .HasColumnType("varbit");
        });
    }
}
