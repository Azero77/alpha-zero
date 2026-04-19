using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.IntegrationEvents;

// Commands sent by the Saga
public record EnrollStudentFromSagaCommand(
    Guid CorrelationId,
    Guid UserId,
    Guid CourseId,
    ResourceArn Resource);

public record AssignStudentRoleFromSagaCommand(
    Guid CorrelationId,
    Guid UserId,
    ResourceArn Course);

// Events published by consumers to notify the Saga
public record StudentEnrolledFromSagaEvent(Guid CorrelationId);
public record StudentRoleAssignedFromSagaEvent(Guid CorrelationId);

// Failure events
public record StudentEnrollmentFailedEvent(Guid CorrelationId, string Reason);
public record StudentRoleAssignmentFailedEvent(Guid CorrelationId, string Reason);
