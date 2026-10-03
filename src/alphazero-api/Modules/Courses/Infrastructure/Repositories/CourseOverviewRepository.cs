using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

internal sealed class CourseOverviewRepository : BaseRepository<AppDbContext, CourseOverview>, ICourseOverviewRepository
{
    public CourseOverviewRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<CourseOverviewDto?> GetByCourseIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        var query = from course in _context.Courses
            join courseOverview in _context.CourseOverviews
                on course.Id equals courseOverview.CourseId
            select new CourseOverviewDto(courseOverview.Id,course.Id, course.Status.ToString(),
                courseOverview.DescriptionContent.Value,
                courseOverview.TargetAudienceContent == null ? null : courseOverview.TargetAudienceContent.Value,
                courseOverview.LearningObjectivesContent == null ? null : courseOverview.LearningObjectivesContent.Value,
                courseOverview.CoverImageDocumentId,
                courseOverview.ETag);
        var result = await query.FirstOrDefaultAsync(cancellationToken);
        return result;
    }
}
