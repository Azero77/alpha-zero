using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetResourceTenant;

public class GetResourceTenantIdHandler(ICourseRepository courseRepository, ISubjectRepository subjectRepository) : IRequestHandler<GetResourceTenantIdQuery, Guid?>
{
    public async Task<Guid?> Handle(GetResourceTenantIdQuery request, CancellationToken cancellationToken)
    {
        return request.Type switch
        {
            ResourceType.Courses => await courseRepository.Entities
                .Where(c => c.Id == request.ResourceId)
                .Select(c => (Guid?)c.TenantId)
                .FirstOrDefaultAsync(cancellationToken),
            
            ResourceType.Subjects => await subjectRepository.Entities
                .Where(s => s.Id == request.ResourceId)
                .Select(s => (Guid?)s.TenantId)
                .FirstOrDefaultAsync(cancellationToken),
            
            _ => null
        };
    }
}
