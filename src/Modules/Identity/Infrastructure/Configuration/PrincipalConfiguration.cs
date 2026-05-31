using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Modules.Identity.Infrastructure.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

internal class PrincipalDataModelConfiguration : IEntityTypeConfiguration<PrincipalDataModel>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new ConditionNodeJsonConverter(), new ResourcePatternJsonConverter() },
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<PrincipalDataModel> builder)
    {
        builder.ToTable("Principals");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Username).IsRequired().HasMaxLength(128);
        builder.HasIndex(p => new { p.Username, p.TenantId }).IsUnique();
        builder.Property(p => p.PasswordHash).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(256);
        builder.Property(p => p.PrincipalType).HasConversion<string>();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.PrincipalScopePattern);

        // Many-to-Many for Managed Policies
        builder.HasMany(p => p.ManagedPolicies)
            .WithMany()
            .UsingEntity<PrincipalPolicyAssignment>(
                j => j.HasOne(a => a.ManagedPolicy).WithMany().HasForeignKey(a => a.ManagedPolicyId),
                j => j.HasOne<PrincipalDataModel>().WithMany().HasForeignKey(a => a.PrincipalId));

        // JSONB for Inline Policies
        builder.Property(p => p.InlinePolicies)
            .HasConversion(
                v => JsonSerializer.Serialize(v, _jsonOptions),
                v => JsonSerializer.Deserialize<List<InlinePolicy>>(v, _jsonOptions) ?? new List<InlinePolicy>(),
                new ValueComparer<List<InlinePolicy>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()))
            .HasColumnType("jsonb");
    }
}
