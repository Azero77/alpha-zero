using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class PrincipalTemplateConfiguration : IEntityTypeConfiguration<PrincipalTemplate>
{
    public void Configure(EntityTypeBuilder<PrincipalTemplate> builder)
    {
        builder.ToTable("Principals");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(256);
        builder.Property(p => p.PrincipalType).HasConversion<string>();
        builder.HasMany(p => p.ManagedPolicies)
            .WithMany()
            .UsingEntity("PrincipalManagedPolicies");
    }
}

public class PrincipalConfiguration : IEntityTypeConfiguration<Principal>
{
    public void Configure(EntityTypeBuilder<Principal> builder)
    {
        builder.Property(p => p.TenantUserId).IsRequired();
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
        builder.HasIndex(p => new { p.ResourceId, p.ScopeResourceType });

        builder.HasMany(p => p.InlinePolicies)
               .WithOne()
               .HasForeignKey("PrincipalId")
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.InlinePolicies)
               .UsePropertyAccessMode(PropertyAccessMode.Field)
               .HasField("_inlinePolicies");
    }
}
