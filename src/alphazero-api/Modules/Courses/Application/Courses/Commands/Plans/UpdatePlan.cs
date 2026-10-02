using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;

public record UpdatePlanCommand(Guid CourseId, Guid PlanId, string Name, Guid PrincipalId) : ICommand<Success>;

public class UpdatePlanCommandValidator : AbstractValidator<UpdatePlanCommand>
{
    public UpdatePlanCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PrincipalId).NotEmpty();
    }
}

public sealed class UpdatePlanCommandHandler : IRequestHandler<UpdatePlanCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<UpdatePlanCommandHandler> _logger;

    public UpdatePlanCommandHandler(ICourseRepository courseRepository, ILogger<UpdatePlanCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetCourseAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = course.UpdatePlan(request.PlanId, request.Name, request.PrincipalId);
        if (result.IsError) return result.Errors;

        _logger.LogInformation("Plan {PlanId} updated for Course {CourseId}.", request.PlanId, request.CourseId);

        return Result.Success;
    }
}
