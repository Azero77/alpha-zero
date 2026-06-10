using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ISectionRepository : IRepository<CourseSection>
{
}

public interface ICurriculumItemRepository : IRepository<CurriculumItem>
{
}
