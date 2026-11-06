using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Enroll;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Services;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.ProvisionAccessCode;

/// <summary>
/// Provisions course access for a redeemed access code (Library integration event, issue #58).
/// Validates the target ARN, resolves course/plan in the Courses domain, ensures the
/// deterministic Identity principal, and creates the enrollment. Idempotent under
/// at-least-once delivery; domain validation or repeated failures are parked into
/// a retryable FailedProvisioningRecord (via the repository, never raw persistence).
/// Always completes with Success — a parked failure is the business outcome, not an error,
/// so the surrounding transaction commits the park record.
/// </summary>
public record ProvisionAccessCodeCommand(
    Guid AccessCodeId,
    Guid RedeemingUserId,
    ResourceArn? TargetResourceArn,
    Guid TenantId) : ICommand<Success>, ITransactionalCommand;

public class ProvisionAccessCodeCommandValidator : AbstractValidator<ProvisionAccessCodeCommand>
{
    public ProvisionAccessCodeCommandValidator()
    {
        RuleFor(x => x.AccessCodeId).NotEmpty();
        RuleFor(x => x.RedeemingUserId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

public sealed class ProvisionAccessCodeCommandHandler(
    ICourseRepository courseRepository,
    IEnrollmentRepository enrollmentRepository,
    ICoursePlanPrincipalService planPrincipalService,
    IFailedProvisioningRecordRepository failedProvisioningRepository,
    ISender mediator,
    ILogger<ProvisionAccessCodeCommandHandler> logger) : IRequestHandler<ProvisionAccessCodeCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ProvisionAccessCodeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId;
        var accessCodeId = request.AccessCodeId;
        var userId = request.RedeemingUserId;
        var arn = request.TargetResourceArn;
        var arnString = arn?.ToString() ?? string.Empty;

        // 1. Validate TargetResourceArn and extract CourseId
        var courseId = arn?.ExtractCourseId();
        if (courseId is null || courseId == Guid.Empty)
        {
            if (arn != null && arn.IsType("course"))
            {
                try { courseId = arn.GetCourseId(); }
                catch { courseId = null; }
            }
        }

        if (courseId is null || courseId == Guid.Empty)
        {
            await ParkAsync(
                tenantId,
                accessCodeId,
                userId,
                arnString,
                $"Invalid or malformed target resource ARN '{arn}'. Course ID could not be extracted.",
                cancellationToken);
            return Result.Success;
        }

        // 2. Storm-safety & at-least-once idempotency:
        // Check if student is already enrolled in this course
        var existingEnrollment = await enrollmentRepository.GetFirst(
            e => e.StudentId == userId && e.CourseId == courseId.Value,
            cancellationToken);

        if (existingEnrollment != null)
        {
            logger.LogInformation(
                "AccessCode {CodeId} for User {UserId} in Course {CourseId} already has active enrollment {EnrollmentId}. Idempotent duplicate handled.",
                accessCodeId, userId, courseId.Value, existingEnrollment.Id);

            await failedProvisioningRepository.MarkProvisionedIfExistsAsync(tenantId, accessCodeId, cancellationToken);
            return Result.Success;
        }

        // Check if already provisioned in provisioning records
        var existingRecord = await failedProvisioningRepository.GetByTenantAndAccessCodeAsync(
            tenantId, accessCodeId, cancellationToken);

        if (existingRecord != null && existingRecord.Status == ProvisioningStatus.Provisioned)
        {
            logger.LogInformation(
                "AccessCode {CodeId} already marked Provisioned. Idempotent duplicate handled.", accessCodeId);
            return Result.Success;
        }

        // 3. Validate Course existence in Courses domain
        var course = await courseRepository.Query(courseId.Value)
            .WithPlans()
            .ExecuteAsync(cancellationToken);

        if (course is null)
        {
            await ParkAsync(
                tenantId,
                accessCodeId,
                userId,
                arnString,
                $"Course '{courseId.Value}' was not found in Courses domain.",
                cancellationToken);
            return Result.Success;
        }

        // 4. Resolve plan from Courses' own domain
        Domain.Aggregates.Courses.CoursePlan? selectedPlan = null;
        if (arn != null && arn.ResourcePath.Contains("/plan/"))
        {
            try
            {
                var planId = arn.GetResource("plan");
                selectedPlan = course.Plans.FirstOrDefault(p => p.Id == planId);
            }
            catch { }
        }

        if (selectedPlan is null)
        {
            if (course.Plans.Count == 1)
            {
                selectedPlan = course.Plans.Single();
            }
            else if (course.Plans.Count > 1)
            {
                selectedPlan = course.Plans.FirstOrDefault(p => string.Equals(p.Name, "Standard", StringComparison.OrdinalIgnoreCase))
                    ?? course.Plans.First();
            }
        }

        if (selectedPlan is null)
        {
            await ParkAsync(
                tenantId,
                accessCodeId,
                userId,
                arnString,
                $"No applicable plan could be resolved for Course '{courseId.Value}'.",
                cancellationToken);
            return Result.Success;
        }

        // 5. Ensure principal in Identity via deterministic idempotent ensure-principal command
        var mintResult = await planPrincipalService.EnsurePlanPrincipalAsync(
            courseId.Value,
            selectedPlan.Id,
            tenantId,
            cancellationToken);

        if (mintResult.IsError)
        {
            await ParkAsync(
                tenantId,
                accessCodeId,
                userId,
                arnString,
                $"Failed to ensure principal in Identity: {string.Join(", ", mintResult.Errors.Select(e => e.Description))}",
                cancellationToken);
            return Result.Success;
        }

        var principalId = mintResult.Value;

        // 6. Create enrollment via nested command (fires EnrollmentCreated domain & integration events)
        var accessExpiresOn = selectedPlan.AccessWindowDays is int windowDays
            ? DateTime.UtcNow.AddDays(windowDays)
            : (DateTime?)null;

        var coursePlanPrincipalId = selectedPlan.AccessWindowDays is not null
            ? principalId
            : (Guid?)null;

        var enrollCommand = new EnrollInCourseCommand(
            userId,
            courseId.Value,
            accessExpiresOn,
            coursePlanPrincipalId);

        var enrollResult = await mediator.Send(enrollCommand, cancellationToken);

        if (enrollResult.IsError)
        {
            if (enrollResult.FirstError.Code == "Enrollment.Exists")
            {
                logger.LogInformation(
                    "Student {UserId} is already enrolled in Course {CourseId} (Enrollment.Exists). Idempotent duplicate handled.",
                    userId, courseId.Value);

                await failedProvisioningRepository.MarkProvisionedIfExistsAsync(tenantId, accessCodeId, cancellationToken);
                return Result.Success;
            }

            await ParkAsync(
                tenantId,
                accessCodeId,
                userId,
                arnString,
                $"Enrollment creation failed: {string.Join(", ", enrollResult.Errors.Select(e => e.Description))}",
                cancellationToken);
            return Result.Success;
        }

        // 7. Mark successfully provisioned
        if (existingRecord != null)
        {
            await failedProvisioningRepository.MarkProvisionedIfExistsAsync(tenantId, accessCodeId, cancellationToken);
        }

        logger.LogInformation(
            "Successfully provisioned access for User {UserId} in Course {CourseId} (Enrollment {EnrollmentId}, Principal {PrincipalId}) from AccessCode {CodeId}.",
            userId, courseId.Value, enrollResult.Value, principalId, accessCodeId);

        return Result.Success;
    }

    private async Task ParkAsync(
        Guid tenantId,
        Guid accessCodeId,
        Guid userId,
        string targetResourceArn,
        string reason,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Parking failed provisioning for AccessCode {CodeId}, User {UserId}, Resource '{Resource}'. Reason: {Reason}",
            accessCodeId, userId, targetResourceArn, reason);

        await failedProvisioningRepository.ParkFailureAsync(
            tenantId,
            accessCodeId,
            userId,
            targetResourceArn,
            reason,
            cancellationToken);
    }
}
