using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;

public record AddPlanCommand(Guid CourseId, string Name, Guid PrincipalId) : ICommand<Guid>;

public class AddPlanCommandValidator : AbstractValidator<AddPlanCommand>
{
    public AddPlanCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PrincipalId).NotEmpty();
    }
}

public sealed class AddPlanCommandHandler : IRequestHandler<AddPlanCommand, ErrorOr<Guid>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<AddPlanCommandHandler> _logger;

    public AddPlanCommandHandler(ICourseRepository courseRepository, ILogger<AddPlanCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(AddPlanCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetCourseAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var planResult = course.AddPlan(request.Name, request.PrincipalId);
        if (planResult.IsError) return planResult.Errors;

        _courseRepository.Update(course);

        _logger.LogInformation("Plan '{PlanName}' added to Course {CourseId}.", request.Name, request.CourseId);

        return planResult.Value.Id;
    }
}
