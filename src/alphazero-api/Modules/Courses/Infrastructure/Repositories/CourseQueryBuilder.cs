using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class CourseQueryBuilder : ICourseQueryBuilder
{
    private readonly AppDbContext _context;

    private Guid? _courseId;
    private bool _includeAllSections;
    private Guid? _targetSectionId;
    private bool _includeItems;
    private bool _includeResources;
    private bool _includePlans;
    private bool _isSplitQuery;
    private bool _ignoreQueryFilters;

    public CourseQueryBuilder(AppDbContext context)
    {
        _context = context;
    }

    public ICourseQueryBuilder ForCourse(Guid courseId)
    {
        _courseId = courseId;
        return this;
    }

    public ICourseQueryBuilder WithSections()
    {
        _includeAllSections = true;
        return this;
    }

    public ICourseQueryBuilder WithSection(Guid sectionId)
    {
        _targetSectionId = sectionId;
        return this;
    }

    public ICourseQueryBuilder WithItems(bool include = true)
    {
        _includeItems = include;
        return this;
    }

    public ICourseQueryBuilder WithResources(bool include = true)
    {
        _includeResources = include;
        return this;
    }

    public ICourseQueryBuilder WithPlans(bool include = true)
    {
        _includePlans = include;
        return this;
    }

    public ICourseQueryBuilder AsSplitQuery()
    {
        _isSplitQuery = true;
        return this;
    }

    public ICourseQueryBuilder IgnoreQueryFilters()
    {
        _ignoreQueryFilters = true;
        return this;
    }

    public async Task<Course?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await BuildQuery().FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        var query = _context.Courses.AsQueryable();
        if (_courseId.HasValue)
            query = query.Where(c => c.Id == _courseId.Value);
        if (_ignoreQueryFilters)
            query = query.IgnoreQueryFilters();

        return await query.AnyAsync(cancellationToken);
    }

    private IQueryable<Course> BuildQuery()
    {
        // Tracked query by default for aggregate mutations
        var query = _context.Courses.AsQueryable();

        if (_isSplitQuery)
            query = query.AsSplitQuery();

        if (_ignoreQueryFilters)
            query = query.IgnoreQueryFilters();

        if (_courseId.HasValue)
            query = query.Where(c => c.Id == _courseId.Value);

        if (_includeAllSections)
        {
            if (_includeItems && _includeResources)
            {
                query = query.Include(c => c.Sections)
                             .ThenInclude(s => s.Items)
                             .ThenInclude(i => i.Resources);
            }
            else if (_includeItems)
            {
                query = query.Include(c => c.Sections)
                             .ThenInclude(s => s.Items);
            }
            else
            {
                query = query.Include(c => c.Sections);
            }
        }
        else if (_targetSectionId.HasValue)
        {
            if (_includeItems && _includeResources)
            {
                query = query.Include(c => c.Sections.Where(s => s.Id == _targetSectionId.Value))
                             .ThenInclude(s => s.Items)
                             .ThenInclude(i => i.Resources);
            }
            else if (_includeItems)
            {
                query = query.Include(c => c.Sections.Where(s => s.Id == _targetSectionId.Value))
                             .ThenInclude(s => s.Items);
            }
            else
            {
                query = query.Include(c => c.Sections.Where(s => s.Id == _targetSectionId.Value));
            }
        }

        if (_includePlans)
        {
            query = query.Include(c => c.Plans);
        }

        return query;
    }
}
