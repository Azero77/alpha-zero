using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollements.Commands.Enroll;

public record EnrollInCourseCommand(Guid StudentId, Guid CourseId) : ICommand<Guid>;

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
    private readonly IEnrollementRepository _enrollementRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<EnrollInCourseCommandHandler> _logger;

    public EnrollInCourseCommandHandler(
        IEnrollementRepository enrollementRepository,
        ICourseRepository courseRepository,
        ITenantProvider tenantProvider,
        ILogger<EnrollInCourseCommandHandler> logger)
    {
        _enrollementRepository = enrollementRepository;
        _courseRepository = courseRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(EnrollInCourseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        // Verify course exists
        var course = await _courseRepository.GetFirst(c => c.Id == request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        // Check if already enrolled
        var existingEnrollment = await _enrollementRepository.GetFirst(e => e.StudentId == request.StudentId && e.CourseId == request.CourseId, cancellationToken);
        if (existingEnrollment is not null) return Error.Conflict("Enrollment.Exists", "Student is already enrolled in this course.");

        var enrollmentId = Guid.NewGuid();
        var enrollmentResult = Enrollement.Create(enrollmentId, tenantId.Value, request.StudentId, request.CourseId, course.TotalTrackedItems);

        if (enrollmentResult.IsError) return enrollmentResult.Errors;

        _enrollementRepository.Add(enrollmentResult.Value);
        _logger.LogInformation("Student {StudentId} enrolled in Course {CourseId}.", request.StudentId, request.CourseId);

        return enrollmentId;
    }
}
