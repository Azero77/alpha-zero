using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ICourseRepository : IRepository<Course>
{
    ICourseQueryBuilder Query();
    ICourseQueryBuilder Query(Guid courseId);

    Task<bool> IsAssetInUseAsync(Guid courseAssetId, CancellationToken cancellationToken = default);
    Task<(Guid CourseId, int BitIndex)?> GetItemBitIndexByCourseAssetIdAsync(Guid courseAssetId, CancellationToken cancellationToken = default);
    Task<(Guid CourseId, int BitIndex)?> GetItemBitIndexByExternalResourceIdAsync(Guid externalResourceId, CancellationToken cancellationToken = default);
    Task<List<Course>> GetCoursesByCourseAssetIdAsync(Guid courseAssetId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Flat, strongly-typed read model linking a curriculum placement (Layer 3) to its
/// pool asset (Layer 2) and the external resource identity (Layer 1).
/// </summary>
public record CourseResourceDetailsDto(
    Guid CurriculumResourceId,
    Guid CurriculumItemId,
    Guid CourseId,
    Guid CourseAssetId,
    Guid ExternalResourceId, // Actual VideoId / DocumentId in external modules
    ResourceType ResourceType);

public interface ICourseResourceQueryService
{
    Task<CourseResourceDetailsDto?> GetResourceDetailsAsync(
        Guid courseId,
        Guid curriculumResourceId,
        CancellationToken cancellationToken = default);

    Task<CourseResourceDetailsDto?> GetResourceDetailsByCourseAssetIdAsync(
        Guid courseId,
        Guid courseAssetId,
        CancellationToken cancellationToken = default);
}