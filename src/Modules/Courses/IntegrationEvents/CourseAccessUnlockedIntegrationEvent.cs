using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.IntegrationEvents;

public record CourseAccessUnlockedIntegrationEvent(
    Guid UserId,
    ResourceArn Resource,
    string Plan) : IDomainEvent; // Assuming IDomainEvent is the base for integration events too
