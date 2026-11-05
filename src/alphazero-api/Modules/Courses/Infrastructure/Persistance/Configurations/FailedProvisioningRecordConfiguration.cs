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

        // Unique per (tenant, code): provisioning state is a per-code fact (created once, then
        // retried/marked). Unique guard makes the check-then-insert in the activation consumer
        // race-free under at-least-once redelivery — a duplicate insert fails instead of
        // creating shadow rows that would keep the code in the Library provisioning queue
        // forever (see ParkFailedProvisioningAsync conflict handling).
        builder.HasIndex(e => new { e.TenantId, e.AccessCodeId }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.Status });
    }
}
