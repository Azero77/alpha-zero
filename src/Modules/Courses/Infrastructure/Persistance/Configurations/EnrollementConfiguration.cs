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
        });
    }
}
