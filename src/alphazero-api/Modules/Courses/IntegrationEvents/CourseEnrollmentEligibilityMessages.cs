namespace AlphaZero.Modules.Courses.IntegrationEvents;

/// <summary>Payments asks Courses, the access owner, whether checkout would duplicate active access.</summary>
public sealed record CheckActiveCourseEnrollmentRequest(
    Guid TenantId,
    Guid UserId,
    string TargetResourceArn);

/// <summary>A failed Courses lookup is distinct from "not enrolled" so Payments can fail closed.</summary>
public sealed record CheckActiveCourseEnrollmentResponse(
    bool Succeeded,
    bool HasActiveEnrollment,
    string? ErrorCode = null);
