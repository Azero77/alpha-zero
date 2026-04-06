using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Collections;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class EnrollementConfiguration : IEntityTypeConfiguration<Enrollement>
{
    public void Configure(EntityTypeBuilder<Enrollement> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedNever();
        builder.Property(e => e.Status)
            .HasConversion<string>();

        builder.Property(e => e.Progress)
            .HasConversion(
                v => v.Bitmask,
                v => Progress.FromBitmask(v))
            .HasColumnType("varbit");

        builder.HasIndex(e => new { e.StudentId, e.CourseId }).IsUnique();
    }
}
