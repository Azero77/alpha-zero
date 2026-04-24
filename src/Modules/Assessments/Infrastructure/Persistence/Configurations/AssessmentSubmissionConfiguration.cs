using AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlphaZero.Modules.Assessments.Infrastructure.Persistence.Configurations;

public class AssessmentSubmissionConfiguration : IEntityTypeConfiguration<AssessmentSubmission>
{
    public void Configure(EntityTypeBuilder<AssessmentSubmission> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Status).HasConversion<string>();

        // JSONB Mapping
        builder.Property(s => s.Responses)
            .HasColumnType("jsonb");
            
        builder.HasIndex(s => new { s.StudentId, s.AssessmentId });
    }
}
