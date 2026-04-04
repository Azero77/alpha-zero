using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class EnrollementRepository : BaseRepository<AppDbContext, Enrollement>, IEnrollementRepository
{
    public EnrollementRepository(AppDbContext context) : base(context)
    {
    }
}
