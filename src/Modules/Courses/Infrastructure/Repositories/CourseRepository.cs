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

    public async Task<Course?> GetByIdWithSectionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(Guid CourseId, int BitIndex)?> GetItemBitIndexByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var itemInfo = await _context.Courses
            .SelectMany(c => c.Sections)
            .SelectMany(s => s.Items)
            .Where(i => i.ResourceId == resourceId)
            .Select(i => new { i.SectionId, i.BitIndex })
            .FirstOrDefaultAsync(cancellationToken);

        if (itemInfo == null) return null;

        var courseId = await _context.Set<CourseSection>()
            .Where(s => s.Id == itemInfo.SectionId)
            .Select(s => s.CourseId)
            .FirstOrDefaultAsync(cancellationToken);

        return (courseId, itemInfo.BitIndex);
    }
}
