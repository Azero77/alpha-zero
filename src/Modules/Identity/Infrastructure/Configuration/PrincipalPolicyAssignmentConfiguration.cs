using AlphaZero.Modules.Identity.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Identity.Infrastructure.Configuration;

public class PrincipalPolicyAssignmentConfiguration : IEntityTypeConfiguration<PrincipalPolicyAssignment>
{
    public void Configure(EntityTypeBuilder<PrincipalPolicyAssignment> builder)
    {
        builder.ToTable("PrincipalManagedPolicyAssignments");
        builder.HasKey(a => new { a.PrincipalId, a.ManagedPolicyId });

        builder.HasOne(a => a.Principal)
               .WithMany()
               .HasForeignKey(a => a.PrincipalId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.ManagedPolicy)
               .WithMany()
               .HasForeignKey(a => a.ManagedPolicyId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
