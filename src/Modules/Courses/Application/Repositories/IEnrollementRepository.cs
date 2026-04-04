using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface IEnrollementRepository : IRepository<Enrollement>
{
}
