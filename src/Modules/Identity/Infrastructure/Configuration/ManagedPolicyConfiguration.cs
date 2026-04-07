using AlphaZero.Modules.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class ManagedPolicyConfiguration : IEntityTypeConfiguration<ManagedPolicy>
{
    public void Configure(EntityTypeBuilder<ManagedPolicy> builder)
    {
        builder.ToTable("ManagedPolicies");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
        builder.Property(m => m.PolicyName).IsRequired().HasMaxLength(100);

        // Store Templates as JSONB
        builder.Property(m => m.Statements)
               .HasColumnType("jsonb");
    }
}
