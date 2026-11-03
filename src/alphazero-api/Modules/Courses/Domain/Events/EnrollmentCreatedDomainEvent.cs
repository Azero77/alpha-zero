using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

/// <summary>
/// Raised when an enrollment is created. <see cref="CoursePlanPrincipalId"/> is the per-(course,
/// plan) access principal (issue #34 rework §3.2.2, step 9) for enrollments created under a
/// windowed course plan — carried straight onto the integration event for the Identity grant
/// branch; empty for unlimited (shared student template) enrollments. It is a grant-composition
/// fact, not persisted enrollment state.
/// </summary>
public class EnrollmentCreatedDomainEvent : DomainEvent
{
    public Guid EnrollmentId { get; }
    public Guid StudentId { get; }
    public Guid CourseId { get; }
    public Guid TenantId { get; }
    public Guid? CoursePlanPrincipalId { get; }

    public EnrollmentCreatedDomainEvent(Guid enrollmentId, Guid studentId, Guid courseId, Guid tenantId = default, Guid? coursePlanPrincipalId = null)
    {
        EnrollmentId = enrollmentId;
        StudentId = studentId;
        CourseId = courseId;
        TenantId = tenantId;
        CoursePlanPrincipalId = coursePlanPrincipalId;
    }
}
