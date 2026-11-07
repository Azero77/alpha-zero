using AlphaZero.Modules.Library.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Library.Infrastructure.Persistance.Configurations;

public class SaleRecordConfiguration : IEntityTypeConfiguration<SaleRecord>
{
    public void Configure(EntityTypeBuilder<SaleRecord> builder)
    {
        builder.ToTable("SaleRecords");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.LibraryId).IsRequired();
        builder.Property(x => x.AccessCodeId).IsRequired();
        builder.Property(x => x.SoldByUserId).IsRequired();

        builder.Property(x => x.Price)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.SoldAt).IsRequired();

        // One active sale per code; unique index enforces it
        builder.HasIndex(x => x.AccessCodeId).IsUnique();

        // Index for querying sales by library & date
        builder.HasIndex(x => new { x.TenantId, x.LibraryId, x.SoldAt });
    }
}
