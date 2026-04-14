using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class TenantPrinciaplAssignmentConfiguration : IEntityTypeConfiguration<TenantPrinciaplAssignment>
{
    public void Configure(EntityTypeBuilder<TenantPrinciaplAssignment> builder)
    {
        builder.ToTable("TenantPrinciaplAssignments");
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.TenantUser)
            .WithMany()
            .HasForeignKey("TenantUserId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Principal)
            .WithMany()
            .HasForeignKey("PrincipalTemplateId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Resource)
            .HasConversion(
                v => v.ToString(),
                v => ResourceArn.Create(v).Value)
            .HasColumnName("ResourceArn")
            .IsRequired();

        builder.Property(a => a.TenantId).IsRequired();
        
        builder.HasIndex("TenantUserId", "PrincipalTemplateId", "ResourceArn").IsUnique();
    }
}
