using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Library.Infrastructure.Persistance.Configurations;

public class RedemptionAuditLogConfiguration : IEntityTypeConfiguration<RedemptionAuditLog>
{
    public void Configure(EntityTypeBuilder<RedemptionAuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.AccessCodeId).IsRequired();
        builder.Property(x => x.RedeemedByUserId).IsRequired();
        
        builder.Property(x => x.StrategyId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.TargetResourceArn)
            .HasConversion(
                v => v.Value,
                v => ResourceArn.Create(v).Value)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(64);

        builder.Property(x => x.DeviceFingerprint)
            .HasMaxLength(256);

        // Composite index for the primary query pattern
        builder.HasIndex(x => new { x.TenantId, x.LibraryId, x.RedeemedAt });
        
        // One log per redemption event
        builder.HasIndex(x => x.AccessCodeId).IsUnique();
    }
}
