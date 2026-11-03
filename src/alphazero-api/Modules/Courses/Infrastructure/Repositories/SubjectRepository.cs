using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class SubjectRepository : BaseRepository<AppDbContext, Subject>, ISubjectRepository
{
    public SubjectRepository(AppDbContext context) : base(context)
    {
    }

    // Tracked on purpose: the subject update/delete commands mutate the loaded aggregate.
    public async Task<Subject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Subjects
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }
}
