using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class CourseRepository : BaseRepository<AppDbContext, Course>, ICourseRepository
{
    public CourseRepository(AppDbContext context) : base(context)
    {
    }

    public ICourseQueryBuilder Query() => new CourseQueryBuilder(_context);

    public ICourseQueryBuilder Query(Guid courseId) => new CourseQueryBuilder(_context).ForCourse(courseId);

    public async Task<bool> IsAssetInUseAsync(Guid courseAssetId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<CurriculumResource>()
            .AnyAsync(r => r.CourseAssetId == courseAssetId, cancellationToken);
    }

    public async Task<(Guid CourseId, int BitIndex)?> GetItemBitIndexByCourseAssetIdAsync(Guid courseAssetId, CancellationToken cancellationToken = default)
    {
        var itemInfo = await _context.Courses
            .SelectMany(c => c.Sections)
            .SelectMany(s => s.Items)
            .Where(i => i.Resources.Any(r => r.CourseAssetId == courseAssetId))
            .Select(i => new { i.SectionId, i.BitIndex })
            .FirstOrDefaultAsync(cancellationToken);

        if (itemInfo == null) return null;

        var courseId = await _context.Set<CourseSection>()
            .Where(s => s.Id == itemInfo.SectionId)
            .Select(s => s.CourseId)
            .FirstOrDefaultAsync(cancellationToken);

        return (courseId, itemInfo.BitIndex);
    }

    public async Task<(Guid CourseId, int BitIndex)?> GetItemBitIndexByExternalResourceIdAsync(Guid externalResourceId, CancellationToken cancellationToken = default)
    {
        // NOTE: ResourceArn is a value-converted column, so member access into it
        // (e.g. arn => arn.Value.EndsWith(...)) cannot be translated by EF Core.
        // We resolve the pool asset (Layer 2) bound to the external resource id (Layer 1)
        // by matching the trailing ARN segment client-side, then reuse the asset-based lookup.
        var boundAssets = await _context.CourseAssets
            .Select(a => new { a.Id, a.ResourceArn })
            .ToListAsync(cancellationToken);

        var courseAssetId = boundAssets
            .Where(a => a.ResourceArn.Value.EndsWith($"/{externalResourceId}"))
            .Select(a => (Guid?)a.Id)
            .FirstOrDefault();

        if (courseAssetId is null) return null;

        return await GetItemBitIndexByCourseAssetIdAsync(courseAssetId.Value, cancellationToken);
    }

    public async Task<List<Course>> GetCoursesByCourseAssetIdAsync(Guid courseAssetId, CancellationToken cancellationToken = default)
    {
        return await _context.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Items)
            .Where(c => c.Sections.Any(s => s.Items.Any(i => i.Resources.Any(r => r.CourseAssetId == courseAssetId))))
            .ToListAsync(cancellationToken);
    }
}

public class CourseResourceQueryService : ICourseResourceQueryService
{
    private readonly AppDbContext _context;

    public CourseResourceQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CourseResourceDetailsDto?> GetResourceDetailsAsync(
        Guid courseId,
        Guid curriculumResourceId,
        CancellationToken cancellationToken = default)
    {
        var query = from course in _context.Courses
                    where course.Id == courseId
                    from section in course.Sections
                    from item in section.Items
                    from resource in item.Resources
                    where resource.Id == curriculumResourceId && resource.CourseAssetId != null
                    join asset in _context.CourseAssets
                        on resource.CourseAssetId equals asset.Id
                    select new CourseResourceDetailsDto(
                        resource.Id,
                        item.Id,
                        course.Id,
                        asset.Id,
                        asset.ResourceArn.ExtractResourceId() ?? Guid.Empty,
                        resource.ResourceType);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CourseResourceDetailsDto?> GetResourceDetailsByCourseAssetIdAsync(
        Guid courseId,
        Guid courseAssetId,
        CancellationToken cancellationToken = default)
    {
        var query = from course in _context.Courses
                    where course.Id == courseId
                    from section in course.Sections
                    from item in section.Items
                    from resource in item.Resources
                    where resource.CourseAssetId == courseAssetId
                    join asset in _context.CourseAssets
                        on resource.CourseAssetId equals asset.Id
                    select new CourseResourceDetailsDto(
                        resource.Id,
                        item.Id,
                        course.Id,
                        asset.Id,
                        asset.ResourceArn.ExtractResourceId() ?? Guid.Empty,
                        resource.ResourceType);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }
}