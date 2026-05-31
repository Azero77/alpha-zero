using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class TenantUserPrinciaplAssignmentConfiguration : IEntityTypeConfiguration<TenantUserPrinciaplAssignment>
{
    public void Configure(EntityTypeBuilder<TenantUserPrinciaplAssignment> builder)
    {
        builder.ToTable("TenantPrinciaplAssignments");
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.TenantUser)
            .WithMany()
            .HasForeignKey("TenantUserId")
            .OnDelete(DeleteBehavior.Cascade);

        // Map to PrincipalDataModel via shadow property/foreign key
        builder.HasOne<PrincipalDataModel>()
            .WithMany()
            .HasForeignKey("PrincipalId")
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
