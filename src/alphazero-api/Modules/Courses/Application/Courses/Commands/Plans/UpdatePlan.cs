using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Services;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;

/// <summary>
/// Updates a plan. Plan edits are FUTURE-ONLY (rework §3.2.5): never rewritten onto existing
/// enrollments. Principal handling splits by branch (step 9, §3.2.1):
/// - windowed plans own their per-(course, plan) principal — minted via <see cref="ICoursePlanPrincipalService"/>
///   (MassTransit request-response via IBus) on the first windowed update, then kept across window-day edits (the window rides the
///   enrollment/token, not the policy);
/// - unlimited plans keep the caller-controlled principal (existing one when omitted — legacy behavior).
/// </summary>
public record UpdatePlanCommand(Guid CourseId, Guid PlanId, string Name, Guid? PrincipalId, int? AccessWindowDays = null) : ICommand<Success>;

public class UpdatePlanCommandValidator : AbstractValidator<UpdatePlanCommand>
{
    public UpdatePlanCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}

public sealed class UpdatePlanCommandHandler : IRequestHandler<UpdatePlanCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICoursePlanPrincipalService _planPrincipalService;
    private readonly ILogger<UpdatePlanCommandHandler> _logger;

    public UpdatePlanCommandHandler(
        ICourseRepository courseRepository,
        ICoursePlanPrincipalService planPrincipalService,
        ILogger<UpdatePlanCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _planPrincipalService = planPrincipalService;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .WithPlans()
            .ExecuteAsync(cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var plan = course.Plans.FirstOrDefault(p => p.Id == request.PlanId);
        if (plan is null) return Error.NotFound("Course.Plan", "Plan not found.");

        Guid principalId;
        if (request.AccessWindowDays is not null)
        {
            if (plan.PrincipalId == Guid.Empty)
            {
                // Newly-windowed without a principal (legacy plan gaining a window): mint now —
                // future enrollments under this plan carry the per-(course, plan) principal.
                var mint = await _planPrincipalService.EnsurePlanPrincipalAsync(request.CourseId, plan.Id, course.TenantId, cancellationToken);
                if (mint.IsError) return mint.Errors;

                principalId = mint.Value;
                _logger.LogInformation(
                    "Minted per-(course, plan) principal {PrincipalId} for Course {CourseId} plan {PlanId} on windowed update.",
                    mint.Value, request.CourseId, plan.Id);
            }
            else
            {
                // Already-windowed: the system-owned principal stays — window-day edits are future-only.
                principalId = plan.PrincipalId;
            }
        }
        else
        {
            principalId = request.PrincipalId ?? plan.PrincipalId;
        }

        var result = course.UpdatePlan(request.PlanId, request.Name, principalId, request.AccessWindowDays);
        if (result.IsError) return result.Errors;

        _courseRepository.Update(course);
        _logger.LogInformation("Plan {PlanId} updated for Course {CourseId}.", request.PlanId, request.CourseId);

        return Result.Success;
    }
}
