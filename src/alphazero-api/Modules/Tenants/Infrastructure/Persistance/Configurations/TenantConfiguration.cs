using AlphaZero.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Tenants.Infrastructure.Persistance.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Subdomain)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(x => x.Subdomain).IsUnique();

        builder.Property(x => x.LogoUrl)
            .HasMaxLength(512);

        builder.Property(x => x.PrimaryColor)
            .HasMaxLength(32);

        builder.Property(x => x.SecondaryColor)
            .HasMaxLength(32);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}
