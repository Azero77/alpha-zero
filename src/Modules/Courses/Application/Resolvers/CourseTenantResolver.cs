using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Application.Resolvers;

public class CourseTenantResolver(AppDbContext dbContext) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Courses;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        return await dbContext.Courses
            .Where(c => c.Id == resourceId)
            .Select(c => (Guid?)c.TenantId)
            .FirstOrDefaultAsync(ct);
    }
}
