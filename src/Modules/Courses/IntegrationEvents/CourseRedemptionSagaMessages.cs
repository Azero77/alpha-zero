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

// --- Revocation Saga Messages ---

// Commands
public record RevokeStudentEnrollmentFromSagaCommand(
    Guid CorrelationId,
    Guid UserId,
    Guid CourseId);

public record RemoveStudentRoleFromSagaCommand(
    Guid CorrelationId,
    Guid UserId,
    ResourceArn Course);

// Events
public record StudentEnrollmentRevokedFromSagaEvent(Guid CorrelationId);
public record StudentRoleRemovedFromSagaEvent(Guid CorrelationId);

// Failures
public record StudentEnrollmentRevocationFailedEvent(Guid CorrelationId, string Reason);
public record StudentRoleRemovalFailedEvent(Guid CorrelationId, string Reason);
