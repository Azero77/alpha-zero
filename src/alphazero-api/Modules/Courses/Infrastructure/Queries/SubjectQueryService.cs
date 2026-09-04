using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AlphaZero.Modules.Courses.Infrastructure.Queries;

public class SubjectQueryService : ISubjectQueryService
{
    private readonly AppDbContext _context;

    public SubjectQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<SubjectDto>> ListSubjectsAsync(int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Subject>().AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(s => new SubjectDto(s.Id, s.Name, s.Description))
            .ToListAsync(cancellationToken);

        return new PagedResult<SubjectDto>(items, totalCount, page, perPage);
    }
}
