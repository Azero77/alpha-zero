using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.IntegrationEvents;

public record CourseAccessUnlockedIntegrationEvent(
    Guid Id,
    Guid AccessCodeId,
    DateTime OccuredOn,
    Guid UserId,
    ResourceArn Resource,
    string Plan);


public record UserEnrolledInCourseIntegrationEvent(Guid UserId, ResourceArn Course);
