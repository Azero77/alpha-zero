namespace AlphaZero.Modules.Courses.IntegrationEvents;

/// <summary>
/// Raised when a student enrolls in a course (Courses → Identity grant, rework §3.2.2).
/// <see cref="CoursePlanPrincipalId"/> carries the per-(course, plan) access principal (step 9):
/// non-empty when the enrollment was created under a windowed plan — the Identity consumer then
/// assigns THAT principal at the course ARN; empty keeps the shared student template
/// (unlimited access, unchanged).
/// </summary>
public record EnrollmentCreatedIntegrationEvent(
    Guid EnrollmentId,
    Guid StudentId,
    Guid CourseId,
    Guid TenantId = default,
    Guid? CoursePlanPrincipalId = null);
