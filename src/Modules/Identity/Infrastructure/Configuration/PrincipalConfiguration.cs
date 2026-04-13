using AlphaZero.Modules.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class PrincipalConfiguration : IEntityTypeConfiguration<Principal>
{
    public void Configure(EntityTypeBuilder<Principal> builder)
    {
        builder.ToTable("Principals");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantUserId).IsRequired().HasMaxLength(128);
        builder.HasIndex(p => p.TenantUserId);
        builder.Property(p => p.PrincipalType).HasConversion<string>();
        builder.Property(p => p.PrincipalScopeUrn).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();
        
        // Mapping explicit resource scope indexing
        builder.Property(p => p.ResourceId);
        builder.Property(p => p.ScopeResourceType).HasConversion<string>();
        
        builder.HasIndex(p => new { p.ResourceId, p.ScopeResourceType });

        // Correct way: Map the public property and specify the backing field
        builder.HasMany(p => p.InlinePolicies)
               .WithOne()
               .HasForeignKey("PrincipalId")
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.InlinePolicies)
               .UsePropertyAccessMode(PropertyAccessMode.Field)
               .HasField("_inlinePolicies");
    }
}
