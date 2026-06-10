using AlphaZero.Modules.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.IdentityId).IsRequired().HasMaxLength(128);
        builder.HasIndex(u => new { u.IdentityId, u.TenantId }).IsUnique();
        
        builder.Property(u => u.Name).HasMaxLength(256);
        builder.Property(u => u.TenantId).IsRequired();

        builder.Property(u => u.MainDeviceId);
        builder.Property(u => u.LastMainDeviceSwitchDate);

        builder.HasMany(u => u.Devices)
            .WithOne()
            .HasForeignKey(d => d.TenantUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(TenantUser.Devices))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
