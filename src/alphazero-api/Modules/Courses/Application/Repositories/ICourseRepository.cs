using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ICourseRepository : IRepository<Course>
{
    ICourseQueryBuilder Query();
    ICourseQueryBuilder Query(Guid courseId);

    Task<bool> IsAssetInUseAsync(Guid assetId, CancellationToken cancellationToken = default);
    Task<(Guid CourseId, int BitIndex)?> GetItemBitIndexByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<List<Course>> GetCoursesByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
}


public interface ICourseResourceQueryService
{
    Task<ResourceDTO?> GetResourceInCourse(Guid resourceId, Guid courseId, CancellationToken cancellationToken = default);
}

public record ResourceDTO(Guid ResourceId, Guid ItemId, Guid CourseId, Guid? AssetId, ResourceType ResourceType); 