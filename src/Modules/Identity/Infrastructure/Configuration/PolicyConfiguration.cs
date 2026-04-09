using System.Text.Json;
using AlphaZero.Modules.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.TenantId).IsRequired();

        // Store the list of Statements as a JSONB column in Postgres using explicit serialization
        builder.Property(p => p.Statements)
               .HasConversion(
                   v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                   v => JsonSerializer.Deserialize<List<PolicyStatement>>(v, (JsonSerializerOptions)null!) ?? new List<PolicyStatement>(),
                   new ValueComparer<IReadOnlyCollection<PolicyStatement>>(
                       (c1, c2) => c1!.SequenceEqual(c2!),
                       c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                       c => c.ToList()))
               .HasColumnType("jsonb")
               .HasField("_statements");
    }
}
