using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Application.Resolvers;

public class CourseTenantResolver(ICourseRepository courseRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Courses;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var course = await courseRepository.GetById(resourceId);
        return course?.TenantId;
    }
}

public class SubjectTenantResolver(ISubjectRepository subjectRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Subjects;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var subject = await subjectRepository.GetById(resourceId);
        return subject?.TenantId;
    }
}

public class SectionTenantResolver(ISectionRepository sectionRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Sections;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var section = await sectionRepository.GetById(resourceId);
        return section?.TenantId;
    }
}

public class LessonTenantResolver(IItemRepository itemRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Lessons;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var item = await itemRepository.GetById(resourceId);
        return item?.TenantId;
    }
}
