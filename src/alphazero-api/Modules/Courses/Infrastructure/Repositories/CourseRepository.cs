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
        var query = BuildResourceDetailsQuery(
            courseId,
            resource => resource.Id == curriculumResourceId);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<CourseResourceDetailsDto> BuildResourceDetailsQuery(
        Guid courseId,
        System.Linq.Expressions.Expression<Func<CurriculumResource, bool>> resourceFilter)
    {
        // The predicate must stay an expression for EF translation, so it is applied to the
        // strongly-typed CurriculumResource set first; placements then join by resource id.
        var matchingResourceIds = _context.Set<CurriculumResource>()
            .Where(resourceFilter)
            .Where(r => r.CourseAssetId != null)
            .Select(r => r.Id);

        return from course in _context.Courses
               where course.Id == courseId
               from section in course.Sections
               from item in section.Items
               from resource in item.Resources
               where matchingResourceIds.Contains(resource.Id)
               join asset in _context.CourseAssets
                   on resource.CourseAssetId equals asset.Id
               select new CourseResourceDetailsDto(
                   resource.Id,
                   item.Id,
                   course.Id,
                   asset.Id,
                   asset.ResourceArn,
                   resource.ResourceType);
    }
}