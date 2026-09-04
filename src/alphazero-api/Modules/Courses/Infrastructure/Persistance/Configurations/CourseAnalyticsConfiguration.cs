using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class CourseAnalyticsConfiguration : IEntityTypeConfiguration<CourseAnalytics>
{
    public void Configure(EntityTypeBuilder<CourseAnalytics> builder)
    {
        builder.HasKey(x => x.CourseId);
        builder.HasIndex(x => x.CourseId).IsUnique();

        builder.Property(x => x.ItemCompletions)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<int, int>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<int, int>()
            );
    }
}
