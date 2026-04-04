using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class CourseRepository : BaseRepository<AppDbContext, Course>, ICourseRepository
{
    public CourseRepository(AppDbContext context) : base(context)
    {
    }
}
