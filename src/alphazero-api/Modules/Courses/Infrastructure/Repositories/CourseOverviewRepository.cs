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

    public async Task<CourseOverview?> GetByCourseIdAsync(Guid courseId, CancellationToken cancellationToken = default)
    {
        return await _context.CourseOverviews
            .FirstOrDefaultAsync(x => x.CourseId == courseId, cancellationToken);
    }
}
