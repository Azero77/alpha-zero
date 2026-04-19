using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Library.Infrastructure.Persistance.Configurations;

public class AccessCodeConfiguration : IEntityTypeConfiguration<AccessCode>
{
    public void Configure(EntityTypeBuilder<AccessCode> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CodeHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(x => x.CodeHash).IsUnique();

        builder.Property(x => x.StrategyId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.TargetResourceArn)
            .HasConversion(
                v => v.Value,
                v => ResourceArn.Create(v).Value)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.Metadata)
            .HasColumnType("jsonb");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.LibraryId).IsRequired();
        builder.HasIndex(x => x.LibraryId);

        builder.Property(x => x.TenantId).IsRequired();
        builder.HasIndex(x => x.TenantId);
    }
}
