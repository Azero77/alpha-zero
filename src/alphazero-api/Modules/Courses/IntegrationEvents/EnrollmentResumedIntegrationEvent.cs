namespace AlphaZero.Modules.Courses.IntegrationEvents;

/// <summary>
/// Fired when an enrollment is reactivated (issue #34) — access restored after a
/// suspension, deactivation, or expiry. Announced so downstream systems (billing,
/// gamification) react to lifecycle changes symmetrically with revocation.
/// Wire identity: natural type-name URN per #31's final decision (no MessageUrn pin).
/// </summary>
public record EnrollmentResumedIntegrationEvent(
    Guid EnrollmentId,
    Guid StudentId,
    Guid CourseId,
    Guid TenantId);
