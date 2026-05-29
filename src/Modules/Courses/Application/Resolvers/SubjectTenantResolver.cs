using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Application.Resolvers;

public class SubjectTenantResolver(AppDbContext dbContext) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Subjects;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        return await dbContext.Subjects
            .Where(s => s.Id == resourceId)
            .Select(s => (Guid?)s.TenantId)
            .FirstOrDefaultAsync(ct);
    }
}
