using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Courses.Infrastructure.Persistance.Configurations;

public class FailedProvisioningRecordConfiguration : IEntityTypeConfiguration<FailedProvisioningRecord>
{
    public void Configure(EntityTypeBuilder<FailedProvisioningRecord> builder)
    {
        builder.ToTable("FailedProvisioningRecords", "Courses");

        builder.Property(e => e.Id)
            .ValueGeneratedNever();
        builder.Property(e => e.AccessCodeId)
            .IsRequired();
        builder.Property(e => e.UserId)
            .IsRequired();
        builder.Property(e => e.TargetResourceArn)
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(e => e.FailureReason)
            .HasMaxLength(1024)
            .IsRequired();
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(e => e.RetryCount)
            .IsRequired();
        builder.Property(e => e.CreatedAt)
            .IsRequired();
        builder.Property(e => e.LastAttemptedAt)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.AccessCodeId });
        builder.HasIndex(e => new { e.TenantId, e.Status });
    }
}
