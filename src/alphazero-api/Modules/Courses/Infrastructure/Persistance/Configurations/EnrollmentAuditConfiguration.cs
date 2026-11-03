using AlphaZero.Modules.Courses.Domain.Audits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class EnrollmentAuditConfiguration : IEntityTypeConfiguration<EnrollmentAuditEntry>
{
    public void Configure(EntityTypeBuilder<EnrollmentAuditEntry> builder)
    {
        builder.ToTable("EnrollmentAuditEntries", "Courses");

        builder.Property(e => e.Id)
            .ValueGeneratedNever();
        builder.Property(e => e.SubjectId);
        builder.Property(e => e.EventKind)
            .HasConversion<string>();
        builder.Property(e => e.ActorId);
        builder.Property(e => e.OccurredOn);

        // jsonb metadata (From, To, cause) — same conversion template as CourseAnalytics (#34 §3.1).
        builder.Property(x => x.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                    c => c.ToDictionary(entry => entry.Key, entry => entry.Value)
                ));

        // Story #9 projection: "latest entry per enrollment" is a (SubjectId, OccurredOn DESC) seek.
        builder.HasIndex(e => new { e.SubjectId, e.OccurredOn });
    }
}
