namespace AlphaZero.Modules.Courses.IntegrationEvents;

/// <summary>
/// Published when a student's enrollment completes all active items of a course (#32).
/// Carries the enrollment course/student identity so cross-module consumers (certificates,
/// gamification, notifications) can react without polling the progress store.
/// Delivered through the module's EF outbox with the same transport guarantees as
/// ItemCompleted/EnrollmentCreated.
/// </summary>
public record CourseCompletedIntegrationEvent(
    Guid EnrollmentId,
    Guid CourseId,
    Guid StudentId,
    Guid TenantId = default,
    DateTime CompletedOnUtc = default);
