using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Services;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;

/// <summary>
/// Adds a plan to a course. <see cref="AccessWindowDays"/> non-null makes it a windowed plan:
/// the enrolling flow then delegates the per-(course, plan) access principal minting to Identity
/// through <see cref="ICoursePlanPrincipalService"/> (MassTransit request-response via IBus) and
/// stores the returned id on the plan. Unlimited plans keep the caller-provided principal (unchanged behavior).
/// </summary>
public record AddPlanCommand(Guid CourseId, string Name, Guid? PrincipalId, int? AccessWindowDays = null) : ICommand<Guid>;

public class AddPlanCommandValidator : AbstractValidator<AddPlanCommand>
{
    public AddPlanCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        // Only unlimited plans rely on a caller-provided principal; windowed ones mint one via Identity.
        RuleFor(x => x.PrincipalId).NotNull().NotEmpty().When(x => x.AccessWindowDays is null);
    }
}

public sealed class AddPlanCommandHandler : IRequestHandler<AddPlanCommand, ErrorOr<Guid>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICoursePlanPrincipalService _planPrincipalService;
    private readonly ILogger<AddPlanCommandHandler> _logger;

    public AddPlanCommandHandler(
        ICourseRepository courseRepository,
        ICoursePlanPrincipalService planPrincipalService,
        ILogger<AddPlanCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _planPrincipalService = planPrincipalService;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(AddPlanCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .WithPlans()
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        // Windowed plans mint their dedicated (course, plan) principal first (§3.2.1); the plan id
        // is pre-computed so the mint is keyed on the final plan. Identity is the authority — a
        // failed mint aborts the plan (no windowed plan without its access principal).
        var planId = Guid.NewGuid();
        Guid planPrincipalId;
        if (request.AccessWindowDays is not null)
        {
            var mint = await _planPrincipalService.EnsurePlanPrincipalAsync(request.CourseId, planId, course.TenantId, cancellationToken);
            if (mint.IsError) return mint.Errors;
            planPrincipalId = mint.Value;
        }
        else
        {
            planPrincipalId = request.PrincipalId!.Value; // validator guarantees non-empty for unlimited plans
        }

        var planResult = course.AddPlan(request.Name, planPrincipalId, request.AccessWindowDays, planId);
        if (planResult.IsError) return planResult.Errors;

        _courseRepository.Update(course);

        _logger.LogInformation(
            "Plan '{PlanName}' added to Course {CourseId} (windowed: {IsWindowed}, principal {PrincipalId}).",
            request.Name, request.CourseId, request.AccessWindowDays is not null, planPrincipalId);

        return planResult.Value.Id;
    }
}
