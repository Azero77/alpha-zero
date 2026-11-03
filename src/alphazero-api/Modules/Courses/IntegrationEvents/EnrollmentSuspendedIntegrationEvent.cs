namespace AlphaZero.Modules.Courses.IntegrationEvents;

/// <summary>
/// Fired when an enrollment is suspended (issue #34). Reversible pause of access — consumers
/// should treat access as temporarily withheld (e.g., billing disputes), not permanently revoked.
/// Wire identity: natural type-name URN per #31's final decision (no MessageUrn pin).
/// </summary>
public record EnrollmentSuspendedIntegrationEvent(
    Guid EnrollmentId,
    Guid StudentId,
    Guid CourseId,
    Guid TenantId,
    string? Reason);
