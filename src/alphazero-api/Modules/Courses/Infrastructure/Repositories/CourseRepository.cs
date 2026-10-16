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

    public async Task<bool> IsAssetInUseAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<CurriculumResource>()
            .AnyAsync(r => r.CourseAssetId == assetId, cancellationToken);
    }

    public async Task<(Guid CourseId, int BitIndex)?> GetItemBitIndexByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var itemInfo = await _context.Courses
            .SelectMany(c => c.Sections)
            .SelectMany(s => s.Items)
            .Where(i => i.Resources.Any(r => r.CourseAssetId == resourceId))
            .Select(i => new { i.SectionId, i.BitIndex })
            .FirstOrDefaultAsync(cancellationToken);

        if (itemInfo == null) return null;

        var courseId = await _context.Set<CourseSection>()
            .Where(s => s.Id == itemInfo.SectionId)
            .Select(s => s.CourseId)
            .FirstOrDefaultAsync(cancellationToken);

        return (courseId, itemInfo.BitIndex);
    }

    public async Task<List<Course>> GetCoursesByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        return await _context.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Items)
            .Where(c => c.Sections.Any(s => s.Items.Any(i => i.Resources.Any(r => r.CourseAssetId == resourceId))))
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

    public async Task<ResourceDTO?> GetResourceInCourse(Guid assetId, Guid courseId, CancellationToken token = default)
    {
        var query = from course in _context.Courses
            join section in _context.CourseSections
                on course.Id equals section.CourseId
            join item in _context.CurriculumItems
                on section.Id equals item.SectionId
            join resource in _context.CurriculumResources
                on item.Id equals resource.CurriculumItemId
            where course.Id == courseId && assetId == resource.CourseAssetId
            select new ResourceDTO(resource.Id, resource.CurriculumItemId,course.Id, resource.CourseAssetId, resource.ResourceType);

        return await query.FirstOrDefaultAsync(token);
    }
}