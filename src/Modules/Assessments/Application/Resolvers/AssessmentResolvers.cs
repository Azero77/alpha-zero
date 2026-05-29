using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Assessments.Application.Resolvers;

public class AssessmentTenantResolver(IAssessmentRepository assessmentRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Assessments;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var assessment = await assessmentRepository.GetById(resourceId);
        return assessment?.TenantId;
    }
}

public class SubmissionTenantResolver(ISubmissionRepository submissionRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Submissions;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var submission = await submissionRepository.GetById(resourceId);
        return submission?.TenantId;
    }
}
