using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Enroll;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Services;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="AccessCodeActivatedIntegrationEvent"/> published by the Library module (issue #58).
/// Validates ARN and plan within the Courses domain, ensures the deterministic IAM principal in Identity,
/// and creates the course enrollment. Idempotent under at-least-once delivery; domain validation or repeated
/// failures are parked into a retryable <see cref="FailedProvisioningRecord"/>.
/// </summary>
public class AccessCodeActivatedConsumer : IConsumer<AccessCodeActivatedIntegrationEvent>
{
    private readonly ICoursesModule _coursesModule;
    private readonly ICourseRepository _courseRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ICoursePlanPrincipalService _planPrincipalService;
    private readonly AppDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAmbientTenantContext _ambientTenant;
    private readonly ILogger<AccessCodeActivatedConsumer> _logger;

    public AccessCodeActivatedConsumer(
        ICoursesModule coursesModule,
        ICourseRepository courseRepository,
        IEnrollmentRepository enrollmentRepository,
        ICoursePlanPrincipalService planPrincipalService,
        AppDbContext dbContext,
        ITenantProvider tenantProvider,
        IAmbientTenantContext ambientTenant,
        ILogger<AccessCodeActivatedConsumer> logger)
    {
        _coursesModule = coursesModule;
        _courseRepository = courseRepository;
        _enrollmentRepository = enrollmentRepository;
        _planPrincipalService = planPrincipalService;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _ambientTenant = ambientTenant;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AccessCodeActivatedIntegrationEvent> context)
    {
        var msg = context.Message;

        var tenantId = msg.TenantId != Guid.Empty
            ? msg.TenantId
            : (Guid.TryParse(msg.TargetResourceArn?.TenantIdString, out var parsedTenant) && parsedTenant != Guid.Empty
                ? parsedTenant
                : (_tenantProvider.GetTenant() ?? Guid.Empty));

        // Push ambient tenant scope for background message execution
        using var ambientScope = tenantId != Guid.Empty ? _ambientTenant.Push(tenantId) : null;

        // 1. Validate TargetResourceArn and extract CourseId
        var courseId = msg.TargetResourceArn?.ExtractCourseId();
        if (courseId is null || courseId == Guid.Empty)
        {
            if (msg.TargetResourceArn != null && msg.TargetResourceArn.IsType("course"))
            {
                try { courseId = msg.TargetResourceArn.GetCourseId(); }
                catch { courseId = null; }
            }
        }

        if (courseId is null || courseId == Guid.Empty)
        {
            await ParkFailedProvisioningAsync(
                tenantId,
                msg.AccessCodeId,
                msg.RedeemingUserId,
                msg.TargetResourceArn?.ToString() ?? string.Empty,
                $"Invalid or malformed target resource ARN '{msg.TargetResourceArn}'. Course ID could not be extracted.",
                context.CancellationToken);
            return;
        }

        // 2. Storm-safety & at-least-once idempotency:
        // Check if student is already enrolled in this course
        var existingEnrollment = await _enrollmentRepository.GetFirst(
            e => e.StudentId == msg.RedeemingUserId && e.CourseId == courseId.Value,
            context.CancellationToken);

        if (existingEnrollment != null)
        {
            _logger.LogInformation(
                "AccessCode {CodeId} for User {UserId} in Course {CourseId} already has active enrollment {EnrollmentId}. Idempotent duplicate handled.",
                msg.AccessCodeId, msg.RedeemingUserId, courseId.Value, existingEnrollment.Id);

            await MarkProvisionedIfExistsAsync(tenantId, msg.AccessCodeId, context.CancellationToken);
            return;
        }

        // Check if already provisioned in provisioning records
        var existingRecord = await _dbContext.FailedProvisioningRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.AccessCodeId == msg.AccessCodeId, context.CancellationToken);

        if (existingRecord != null && existingRecord.Status == ProvisioningStatus.Provisioned)
        {
            _logger.LogInformation(
                "AccessCode {CodeId} already marked Provisioned. Idempotent duplicate handled.", msg.AccessCodeId);
            return;
        }

        // 3. Validate Course existence in Courses domain
        var course = await _courseRepository.Query(courseId.Value)
            .WithPlans()
            .ExecuteAsync(context.CancellationToken);

        if (course is null)
        {
            await ParkFailedProvisioningAsync(
                tenantId,
                msg.AccessCodeId,
                msg.RedeemingUserId,
                msg.TargetResourceArn?.ToString() ?? string.Empty,
                $"Course '{courseId.Value}' was not found in Courses domain.",
                context.CancellationToken);
            return;
        }

        // 4. Resolve plan from Courses' own domain
        Domain.Aggregates.Courses.CoursePlan? selectedPlan = null;
        if (msg.TargetResourceArn != null && msg.TargetResourceArn.ResourcePath.Contains("/plan/"))
        {
            try
            {
                var planId = msg.TargetResourceArn.GetResource("plan");
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
            await ParkFailedProvisioningAsync(
                tenantId,
                msg.AccessCodeId,
                msg.RedeemingUserId,
                msg.TargetResourceArn?.ToString() ?? string.Empty,
                $"No applicable plan could be resolved for Course '{courseId.Value}'.",
                context.CancellationToken);
            return;
        }

        // 5. Ensure principal in Identity via deterministic idempotent ensure-principal command
        var mintResult = await _planPrincipalService.EnsurePlanPrincipalAsync(
            courseId.Value,
            selectedPlan.Id,
            tenantId,
            context.CancellationToken);

        if (mintResult.IsError)
        {
            await ParkFailedProvisioningAsync(
                tenantId,
                msg.AccessCodeId,
                msg.RedeemingUserId,
                msg.TargetResourceArn?.ToString() ?? string.Empty,
                $"Failed to ensure principal in Identity: {string.Join(", ", mintResult.Errors.Select(e => e.Description))}",
                context.CancellationToken);
            return;
        }

        var principalId = mintResult.Value;

        // 6. Create enrollment via module command (fires EnrollmentCreated domain & integration events)
        var accessExpiresOn = selectedPlan.AccessWindowDays is int windowDays
            ? DateTime.UtcNow.AddDays(windowDays)
            : (DateTime?)null;

        var coursePlanPrincipalId = selectedPlan.AccessWindowDays is not null
            ? principalId
            : (Guid?)null;

        var enrollCommand = new EnrollInCourseCommand(
            msg.RedeemingUserId,
            courseId.Value,
            accessExpiresOn,
            coursePlanPrincipalId);

        var enrollResult = await _coursesModule.Send(enrollCommand, context.CancellationToken);

        if (enrollResult.IsError)
        {
            if (enrollResult.FirstError.Code == "Enrollment.Exists")
            {
                _logger.LogInformation(
                    "Student {UserId} is already enrolled in Course {CourseId} (Enrollment.Exists). Idempotent duplicate handled.",
                    msg.RedeemingUserId, courseId.Value);

                await MarkProvisionedIfExistsAsync(tenantId, msg.AccessCodeId, context.CancellationToken);
                return;
            }

            await ParkFailedProvisioningAsync(
                tenantId,
                msg.AccessCodeId,
                msg.RedeemingUserId,
                msg.TargetResourceArn?.ToString() ?? string.Empty,
                $"Enrollment creation failed: {string.Join(", ", enrollResult.Errors.Select(e => e.Description))}",
                context.CancellationToken);
            return;
        }

        // 7. Mark successfully provisioned
        if (existingRecord != null)
        {
            existingRecord.MarkProvisioned();
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }

        _logger.LogInformation(
            "Successfully provisioned access for User {UserId} in Course {CourseId} (Enrollment {EnrollmentId}, Principal {PrincipalId}) from AccessCode {CodeId}.",
            msg.RedeemingUserId, courseId.Value, enrollResult.Value, principalId, msg.AccessCodeId);
    }

    private async Task ParkFailedProvisioningAsync(
        Guid tenantId,
        Guid accessCodeId,
        Guid userId,
        string targetResourceArn,
        string reason,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Parking failed provisioning for AccessCode {CodeId}, User {UserId}, Resource '{Resource}'. Reason: {Reason}",
            accessCodeId, userId, targetResourceArn, reason);

        var existing = await _dbContext.FailedProvisioningRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.AccessCodeId == accessCodeId, cancellationToken);

        if (existing != null)
        {
            existing.RecordRetryFailure(reason);
        }
        else
        {
            var record = FailedProvisioningRecord.Create(
                tenantId,
                accessCodeId,
                userId,
                targetResourceArn,
                reason);
            _dbContext.FailedProvisioningRecords.Add(record);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkProvisionedIfExistsAsync(
        Guid tenantId,
        Guid accessCodeId,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.FailedProvisioningRecords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.AccessCodeId == accessCodeId, cancellationToken);

        if (existing != null && existing.Status != ProvisioningStatus.Provisioned)
        {
            existing.MarkProvisioned();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
