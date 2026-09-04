using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;

public record RemovePlanCommand(Guid CourseId, Guid PlanId) : ICommand<Success>;

public class RemovePlanCommandValidator : AbstractValidator<RemovePlanCommand>
{
    public RemovePlanCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
    }
}

public sealed class RemovePlanCommandHandler : IRequestHandler<RemovePlanCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<RemovePlanCommandHandler> _logger;

    public RemovePlanCommandHandler(ICourseRepository courseRepository, ILogger<RemovePlanCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(RemovePlanCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetCourseAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.RemovePlan(request.PlanId);
        if (result.IsError) return result.Errors;

        _logger.LogInformation("Plan {PlanId} removed from Course {CourseId}.", request.PlanId, request.CourseId);

        return Result.Success;
    }
}
