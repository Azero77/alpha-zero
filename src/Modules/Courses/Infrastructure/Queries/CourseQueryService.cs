using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AlphaZero.Modules.Courses.Infrastructure.Queries;

public class CourseQueryService : ICourseQueryService
{
    private readonly AppDbContext _context;

    public CourseQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CourseDto?> GetCourseByIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Course>()
            .AsNoTracking()
            .AsSplitQuery()
            .Where(c => c.Id == courseId)
            .Select(course => new CourseDto(
                course.Id,
                course.Title,
                course.Description,
                course.SubjectId,
                course.Status.ToString(),
                course.Sections.OrderBy(s => s.Order).Select(s => new SectionDto(
                    s.Id,
                    s.Title,
                    s.Order,
                    s.Items.OrderBy(i => i.Order).Select(i => new ItemDto(
                        i.Id,
                        i.Title,
                        i.MainType,
                        i.Order,
                        i.BitIndex,
                        i.Resources.OrderBy(r => r.Order).Select(r => new ResourceDto(
                            r.Arn.Value,
                            r.Type,
                            r.Order,
                            r.Metadata)).ToList())).ToList())).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<CourseSummaryDto>> ListCoursesAsync(Guid? subjectId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Course>().AsNoTracking();

        if (subjectId.HasValue)
        {
            query = query.Where(c => c.SubjectId == subjectId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Title)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(c => new CourseSummaryDto(
                c.Id,
                c.Title,
                c.Description,
                c.SubjectId,
                c.Status.ToString()))
            .ToListAsync(cancellationToken);

        return new PagedResult<CourseSummaryDto>(items, totalCount, page, perPage);
    }
}
