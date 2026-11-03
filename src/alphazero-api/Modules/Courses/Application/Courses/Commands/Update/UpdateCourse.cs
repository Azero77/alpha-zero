using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Update;

public record UpdateCourseCommand(Guid CourseId, string Title, string? Description, Guid SubjectId) : ICommand<Success>;

public class UpdateCourseCommandValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.SubjectId).NotEmpty();
    }
}

public sealed class UpdateCourseCommandHandler(
    ICourseRepository courseRepository,
    ISubjectRepository subjectRepository,
    ITenantProvider tenantProvider,
    ILogger<UpdateCourseCommandHandler> logger) : IRequestHandler<UpdateCourseCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UpdateCourseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var course = await courseRepository.Query(request.CourseId).ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var subjectExists = await subjectRepository.Any(s => s.Id == request.SubjectId && s.TenantId == tenantId.Value, cancellationToken);
        if (!subjectExists) return Error.NotFound("Course.SubjectId", "Provided SubjectId does not exist.");

        course.UpdateInformation(request.Title, request.Description, request.SubjectId);
        courseRepository.Update(course);

        logger.LogInformation("Course {CourseId} updated.", request.CourseId);

        return Result.Success;
    }
}
