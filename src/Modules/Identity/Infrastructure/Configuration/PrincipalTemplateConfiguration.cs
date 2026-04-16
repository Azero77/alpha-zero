using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class PrincipalTemplateConfiguration : IEntityTypeConfiguration<PrincipalTemplate>
{
    public void Configure(EntityTypeBuilder<PrincipalTemplate> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(256);
        builder.Property(p => p.PrincipalType).HasConversion<string>();
        builder.UseTptMappingStrategy();
        // Many-to-Many for ALL Templates (including Principals)
        builder.HasMany(p => p.ManagedPolicies)
            .WithMany()
            .UsingEntity<PrincipalPolicyAssignment>(
                j => j.HasOne(a => a.ManagedPolicy).WithMany().HasForeignKey(a => a.ManagedPolicyId),
                j => j.HasOne(a => a.Principal).WithMany().HasForeignKey(a => a.PrincipalId));
    }
}

public class PrincipalConfiguration : IEntityTypeConfiguration<Principal>
{
    public void Configure(EntityTypeBuilder<Principal> builder)
    {
        builder.Property(p => p.TenantUserId).IsRequired().HasMaxLength(128);
        builder.HasIndex(p => p.TenantUserId);
        builder.Property(p => p.TenantId).IsRequired();

        // Boundary: Converter for ResourcePattern
        builder.Property(p => p.PrincipalScope)
            .HasConversion(
                v => v != null ? v.Value : null,
                v => v != null ? ResourcePattern.Create(v).Value : null)
            .HasColumnName("PrincipalScopePattern");

        builder.Property(p => p.ResourceId);
        builder.Property(p => p.ScopeResourceType).HasConversion<string>();

        // ManagedPolicies are handled in PrincipalTemplateConfiguration (Inherited)

        // JSONB: InlinePolicies (Collection of Policy objects)
        builder.Property(p => p.InlinePolicies)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<Policy>>(v, (JsonSerializerOptions)null!) ?? new List<Policy>(),
                new ValueComparer<IReadOnlyCollection<Policy>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()))
            .HasColumnType("jsonb")
            .HasField("_inlinePolicies");
    }
}
