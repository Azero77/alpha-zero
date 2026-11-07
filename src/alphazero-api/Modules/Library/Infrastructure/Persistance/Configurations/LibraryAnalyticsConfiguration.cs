using AlphaZero.Modules.Library.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AlphaZero.Modules.Library.Infrastructure.Persistance.Configurations;

public class LibraryAnalyticsConfiguration : IEntityTypeConfiguration<LibraryAnalytics>
{
    public void Configure(EntityTypeBuilder<LibraryAnalytics> builder)
    {
        builder.ToTable("LibraryAnalytics");

        // Composite key over tenant/library/batch (sentinel-resolved, all non-null so
        // ON CONFLICT upserts in the refresh service can address a row deterministically).
        builder.HasKey(x => new { x.TenantId, x.LibraryId, x.BatchId });

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.LibraryId).IsRequired();
        builder.Property(x => x.BatchId).IsRequired();

        builder.Property(x => x.TotalRevenue)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(x => x.RevenueByPeriod)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<string, Dictionary<string, decimal>>(),
                new ValueComparer<Dictionary<string, Dictionary<string, decimal>>>(
                    (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                    c => c.ToDictionary(entry => entry.Key, entry => entry.Value)
                ));

        builder.Property(x => x.SalesByPeriod)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<string, int>(),
                new ValueComparer<Dictionary<string, int>>(
                    (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                    c => c.ToDictionary(entry => entry.Key, entry => entry.Value)
                ));
    }
}
