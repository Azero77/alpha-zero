namespace AlphaZero.Modules.Courses.IntegrationEvents;

/// <summary>
/// Fired when an enrollment expires automatically at the end of its course plan's access
/// window (issue #34, non-HTTP background trigger). Downstream consumers may exclude the
/// student from active analytics but should retain the roster entry.
/// Wire identity: natural type-name URN per #31's final decision (no MessageUrn pin).
/// </summary>
public record EnrollmentExpiredIntegrationEvent(
    Guid EnrollmentId,
    Guid StudentId,
    Guid CourseId,
    Guid TenantId);
