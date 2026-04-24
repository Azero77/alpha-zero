using AlphaZero.Modules.Assessments.Domain.Models.Content;
using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;

public class AssessmentVersion : TenantOwnedEntity
{
    public Guid AssessmentId { get; private set; }
    public int VersionNumber { get; private set; }
    public AssessmentContent Content { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private AssessmentVersion() { } // For EF Core

    internal AssessmentVersion(Guid id, Guid tenantId, Guid assessmentId, int versionNumber, AssessmentContent content) 
        : base(id, tenantId)
    {
        AssessmentId = assessmentId;
        VersionNumber = versionNumber;
        Content = content;
        CreatedAt = DateTime.UtcNow;
    }
}
