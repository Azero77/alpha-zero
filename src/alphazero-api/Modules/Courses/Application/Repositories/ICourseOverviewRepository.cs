using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ICourseOverviewRepository : IRepository<CourseOverview>
{
    Task<CourseOverview?> GetByCourseIdAsync(Guid courseId, CancellationToken cancellationToken = default);
}
