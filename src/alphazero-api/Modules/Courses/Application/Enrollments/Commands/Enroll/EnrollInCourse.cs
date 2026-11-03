using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.Enroll;

/// <param name="AccessExpiresOn">Optional expiry instant derived by the enrolling flow (plan access window, issue #34).</param>
/// <param name="CoursePlanPrincipalId">
/// The per-(course, plan) access principal (rework §3.2.2) when enrolling under a windowed plan —
/// carried onto the created event for the Identity grant branch; null for unlimited enrollments.
/// </param>
public record EnrollInCourseCommand(Guid StudentId, Guid CourseId, DateTime? AccessExpiresOn = null, Guid? CoursePlanPrincipalId = null) : ICommand<Guid>;

public class EnrollInCourseCommandValidator : AbstractValidator<EnrollInCourseCommand>
{
    public EnrollInCourseCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public sealed class EnrollInCourseCommandHandler : IRequestHandler<EnrollInCourseCommand, ErrorOr<Guid>>
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<EnrollInCourseCommandHandler> _logger;

    public EnrollInCourseCommandHandler(
        IEnrollmentRepository enrollmentRepository,
        ICourseRepository courseRepository,
        ITenantProvider tenantProvider,
        ILogger<EnrollInCourseCommandHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _courseRepository = courseRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(EnrollInCourseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        // Verify course exists (plans included: self-serve enroll derives its plan below)
        var course = await _courseRepository.Query(request.CourseId)
            .WithSections()
            .WithItems()
            .WithPlans()
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        // Self-serve enroll plan branch (rework §3.2.2): when the course carries exactly one plan,
        // the enrollment rides it — windowed plans freeze the access window at enroll ("they bought
        // N days") and ride the per-(course, plan) principal onto the created event for the Identity
        // grant branch (identical semantics to the purchase-saga consumer). Unlimited/plan-less
        // enrollments keep the legacy shape (null window, shared student-template grant). Multi-plan
        // courses are plan-ambiguous for self-serve enroll — those must go through the purchase
        // saga (EnrollStudentFromSagaConsumer), which selects the plan explicitly.
        var singlePlan = course.Plans.Count == 1 ? course.Plans.Single() : null;
        var accessExpiresOn = singlePlan?.AccessWindowDays is int windowDays
            ? DateTime.UtcNow.AddDays(windowDays)
            : request.AccessExpiresOn;
        var coursePlanPrincipalId = singlePlan?.AccessWindowDays is int _ ? singlePlan.PrincipalId : request.CoursePlanPrincipalId;

        // Check if already enrolled
        var existingEnrollment = await _enrollmentRepository.GetFirst(e => e.StudentId == request.StudentId && e.CourseId == request.CourseId, cancellationToken);
        if (existingEnrollment is not null) return Error.Conflict("Enrollment.Exists", "Student is already enrolled in this course.");

        var enrollmentId = Guid.NewGuid();
        var enrollmentResult = Enrollment.Create(
            enrollmentId,
            tenantId.Value,
            request.StudentId,
            request.CourseId,
            accessExpiresOn,
            coursePlanPrincipalId);

        if (enrollmentResult.IsError) return enrollmentResult.Errors;

        _enrollmentRepository.Add(enrollmentResult.Value);
        _logger.LogInformation("Student {StudentId} enrolled in Course {CourseId}.", request.StudentId, request.CourseId);

        return enrollmentId;
    }
}
