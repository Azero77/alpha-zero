using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Assessments.Infrastructure.Persistence.Configurations;

public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Title).IsRequired().HasMaxLength(256);
        builder.Property(a => a.Description).HasMaxLength(1024);
        
        builder.Property(a => a.Type).HasConversion<string>();
        builder.Property(a => a.Status).HasConversion<string>();

        // JSONB Mapping
        builder.Property(a => a.Content)
            .HasColumnType("jsonb");
    }
}
