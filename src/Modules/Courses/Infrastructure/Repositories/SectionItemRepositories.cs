using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class SectionRepository(AppDbContext context) : BaseRepository<AppDbContext, CourseSection>(context), ISectionRepository
{
}

public class CurriculumItemRepository(AppDbContext context) : BaseRepository<AppDbContext, CurriculumItem>(context), ICurriculumItemRepository
{
}
