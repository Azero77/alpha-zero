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

        builder.Property(p => p.IdentityId).IsRequired().HasMaxLength(128);
        builder.HasIndex(p => p.IdentityId);
        builder.Property(p => p.PrincipalType).HasConversion<string>();
        builder.Property(p => p.PrincipalScopeUrn).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();

        // Mapping the private list of inline policies
        builder.HasMany<Policy>("_inlinePolicies")
               .WithOne()
               .HasForeignKey("PrincipalId")
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.InlinePolicies).Metadata.SetField("_inlinePolicies");
    }
}
