using AlphaZero.Modules.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class ManagedPolicyConfiguration : IEntityTypeConfiguration<ManagedPolicy>
{
    public void Configure(EntityTypeBuilder<ManagedPolicy> builder)
    {
        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(m => m.Name).IsUnique();

        // Store Templates as JSONB using explicit serialization
        builder.Property(m => m.Statements)
               .HasConversion(
                   v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                   v => JsonSerializer.Deserialize<List<PolicyTemplateStatement>>(v, (JsonSerializerOptions)null!) ?? new List<PolicyTemplateStatement>(),
                   new ValueComparer<List<PolicyTemplateStatement>>(
                       (c1, c2) => c1!.SequenceEqual(c2!),
                       c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                       c => c.ToList()))
               .HasColumnType("jsonb");
    }
}
