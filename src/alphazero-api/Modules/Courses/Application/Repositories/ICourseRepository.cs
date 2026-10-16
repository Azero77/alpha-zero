using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ICourseRepository : IRepository<Course>
{
    ICourseQueryBuilder Query();
    ICourseQueryBuilder Query(Guid courseId);

    Task<bool> IsAssetInUseAsync(Guid courseAssetId, CancellationToken cancellationToken = default);
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
    // Member deliberately named after its type (C# "Color/Color" rule). Note: inside this
    // record, a bare `ResourceArn.` resolves to the property, not the type — use a fully
    // qualified name for static factories such as Shared.Domain.ResourceArn.ForVideo(...).
    ResourceArn ResourceArn,
    ResourceType ResourceType)
{
    // Computed on the materialized row (not in the EF query) — keeps the query fully
    // server-translated while preserving the Layer-1 identity for playback tickets.
    public Guid ExternalResourceId => ResourceArn.ExtractResourceId() ?? Guid.Empty;
}

public interface ICourseResourceQueryService
{
    Task<CourseResourceDetailsDto?> GetResourceDetailsAsync(
        Guid courseId,
        Guid curriculumResourceId,
        CancellationToken cancellationToken = default);
}