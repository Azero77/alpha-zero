using AlphaZero.Modules.Library.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Library.Infrastructure.Persistance.Configurations;

public class LibraryAuditLogConfiguration : IEntityTypeConfiguration<LibraryAuditLog>
{
    public void Configure(EntityTypeBuilder<LibraryAuditLog> builder)
    {
        builder.ToTable("LibraryAuditLogs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EventId).IsRequired();
        builder.HasIndex(x => x.EventId).IsUnique();

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.LibraryId).IsRequired(false);
        builder.Property(x => x.ActorUserId).IsRequired(false);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.AggregateId).IsRequired();
        builder.HasIndex(x => x.AggregateId);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.OccurredAt).IsRequired();

        // Index for querying audit stream by tenant, library, and occurrence date
        builder.HasIndex(x => new { x.TenantId, x.LibraryId, x.OccurredAt });
    }
}
