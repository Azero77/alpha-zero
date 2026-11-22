using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public enum CoursePlanCatalogAvailability { Available, Unavailable }

public sealed record CoursePlanCatalogChangedDomainEvent(Guid TenantId, Guid CourseId, Guid CoursePlanId,
    string TargetResourceArn, string Title, CoursePlanCatalogAvailability Availability, long SourceVersion,
    DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}
