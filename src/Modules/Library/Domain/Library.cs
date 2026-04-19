using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Library.Domain;

public class Library : AggregateRoot, IDomainTenantOwned
{
    public string Name { get; private set; } = default!;
    public Guid TenantId { get; private set; }
    
    private readonly List<Guid> _allowedCourseIds = new();
    public IReadOnlyCollection<Guid> AllowedCourseIds => _allowedCourseIds.AsReadOnly();

    private Library() { }

    private Library(Guid id, string name, Guid tenantId) : base(id)
    {
        Name = name;
        TenantId = tenantId;
    }

    public static Library Create(string name, Guid tenantId)
    {
        return new Library(Guid.NewGuid(), name, tenantId);
    }

    public ErrorOr<Success> AuthorizeCourse(Guid courseId)
    {
        if (_allowedCourseIds.Contains(courseId))
            return Error.Conflict("Library.CourseAlreadyAuthorized", "Library is already authorized for this course.");

        _allowedCourseIds.Add(courseId);
        return Result.Success;
    }

    public ErrorOr<Success> DeauthorizeCourse(Guid courseId)
    {
        if (!_allowedCourseIds.Contains(courseId))
            return Error.NotFound("Library.CourseNotAuthorized", "Library is not authorized for this course.");

        _allowedCourseIds.Remove(courseId);
        return Result.Success;
    }
}
