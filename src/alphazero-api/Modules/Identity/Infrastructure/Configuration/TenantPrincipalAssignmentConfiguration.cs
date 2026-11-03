using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class TenantUserPrincipalAssignmentConfiguration : IEntityTypeConfiguration<TenantUserPrincipalAssignment>
{
    public void Configure(EntityTypeBuilder<TenantUserPrincipalAssignment> builder)
    {
        builder.ToTable("TenantPrincipalAssignments");
        builder.HasKey(a => a.Id);

        // Real CLR FK property (TenantUserId on the domain model) — a shadow FK is not queryable
        // from LINQ predicates, forcing navigation traversal that EF cannot translate under
        // query filters (see TenantUserId comment).
        builder.HasOne(a => a.TenantUser)
            .WithMany()
            .HasForeignKey(a => a.TenantUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Map to PrincipalDataModel via the PrincipalId property on the Domain model
        builder.HasOne<PrincipalDataModel>()
            .WithMany()
            .HasForeignKey(a => a.PrincipalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Resource)
            .HasConversion(
                v => v.Value,
                v => ResourceArn.Create(v).Value)
            .HasColumnName("ResourceArn")
            .IsRequired();

        builder.Property(a => a.TenantId).IsRequired();
        
        builder.HasIndex("TenantUserId", "PrincipalId", "Resource").IsUnique();
    }
}
