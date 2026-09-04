using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using MassTransit;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Subject;

public class Subject : TenantOwnedAggregate, ISoftDeletable
{
    private Subject(Guid id, Guid tenantId,string name, string? description) : base(id, tenantId)
    {
        Name = name;
        Description = description;
    }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? OnDeleted { get; private set; }
    

    public static ErrorOr<Subject> Create(Guid id,Guid tenantId, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("Subject.Validation","no name is provided");
        }
        return new Subject(id, tenantId,name, description);
    }

}