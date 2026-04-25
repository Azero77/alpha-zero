
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ICourseRepository : IRepository<Course>
{
    Task<Course?> GetByIdWithSectionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(Guid CourseId, int BitIndex)?> GetItemBitIndexByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<List<Course>> GetCoursesByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default);
}
