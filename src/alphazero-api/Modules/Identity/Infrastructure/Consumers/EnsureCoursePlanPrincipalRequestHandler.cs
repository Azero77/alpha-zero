using AlphaZero.Modules.Identity.Application;
using AlphaZero.Modules.Identity.Application.Principals.Commands.EnsureCoursePlanPrincipal;
using AlphaZero.Modules.Identity.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Infrastructure.Consumers;

/// <summary>
/// MassTransit request-response consumer that handles per-(course, plan) principal provisioning
/// requests from orchestrating modules (such as Courses) over the module bus.
/// </summary>
public class EnsureCoursePlanPrincipalRequestHandler : IConsumer<EnsureCoursePlanPrincipalRequest>
{
    private readonly IIdentityModule _identityModule;
    private readonly ILogger<EnsureCoursePlanPrincipalRequestHandler> _logger;

    public EnsureCoursePlanPrincipalRequestHandler(
        IIdentityModule identityModule,
        ILogger<EnsureCoursePlanPrincipalRequestHandler> logger)
    {
        _identityModule = identityModule;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EnsureCoursePlanPrincipalRequest> context)
    {
        var msg = context.Message;
        var result = await _identityModule.Send(
            new EnsureCoursePlanPrincipalCommand(msg.CourseId, msg.PlanId, msg.TenantId),
            context.CancellationToken);

        if (result.IsError)
        {
            var first = result.FirstError;
            _logger.LogError(
                "Failed to ensure course plan principal for Course {CourseId} Plan {PlanId}: {Error}",
                msg.CourseId, msg.PlanId, first.Description);

            await context.RespondAsync(new CoursePlanPrincipalEnsureFailedResponse(
                first.Description,
                first.Code));
            return;
        }

        await context.RespondAsync(new CoursePlanPrincipalEnsuredResponse(result.Value));
    }
}
