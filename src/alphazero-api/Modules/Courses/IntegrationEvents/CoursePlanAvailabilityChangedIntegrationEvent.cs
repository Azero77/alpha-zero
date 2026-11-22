namespace AlphaZero.Modules.Courses.IntegrationEvents;

public enum CoursePlanAvailability
{
    Available,
    Unavailable
}

/// <summary>
/// Full source snapshot for one Course Plan. SourceVersion is a durable per-plan counter,
/// incremented whenever any published-sellability fact changes, so Payments can reject reordering.
/// </summary>
public sealed record CoursePlanAvailabilityChangedIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    Guid CourseId,
    Guid CoursePlanId,
    string TargetResourceArn,
    string Title,
    CoursePlanAvailability Availability,
    long SourceVersion,
    DateTime OccurredAt);
