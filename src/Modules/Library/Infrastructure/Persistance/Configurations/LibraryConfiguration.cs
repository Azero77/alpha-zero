using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AlphaZero.Modules.Library.Infrastructure.Persistance.Configurations;

public class LibraryConfiguration : IEntityTypeConfiguration<Domain.Library>
{
    public void Configure(EntityTypeBuilder<Domain.Library> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Address)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.ContactNumber)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.TenantId).IsRequired();
        builder.HasIndex(x => x.TenantId);
        builder.Navigation(x => x.AllowedResources)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(x => x.AllowedResources, resourceBuilder =>
        {
            resourceBuilder.ToTable("LibraryAllowedResources");
            resourceBuilder.WithOwner().HasForeignKey("LibraryId");
            resourceBuilder.HasKey("LibraryId", "Value");
            resourceBuilder.Property(r => r.Value)
            .HasColumnName("ResourcePatternValue")
            .IsRequired();
        });
    }
}
