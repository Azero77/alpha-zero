using AlphaZero.Modules.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.HasKey(d => d.Id);
        
        builder.Property(d => d.DeviceName).HasMaxLength(256);
        builder.Property(d => d.Platform).HasConversion<string>();
        builder.Property(d => d.PublicKey).IsRequired();
        builder.Property(d => d.RegisteredAt).IsRequired();
    }
}
