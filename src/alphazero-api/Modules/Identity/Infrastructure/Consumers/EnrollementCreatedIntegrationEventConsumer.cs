using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Identity.Application;
using AlphaZero.Modules.Identity.Application.Principals.Commands.AssignPrincipalToUser;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Infrastructure.Consumers;

public class EnrollementCreatedIntegrationEventConsumer : IConsumer<EnrollementCreatedIntegrationEvent>
{
    public static readonly Guid StudentPrincipalTemplateId = Guid.Parse("00000000-0000-0000-0000-100000000002");

    private readonly IIdentityModule _identityModule;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<EnrollementCreatedIntegrationEventConsumer> _logger;

    public EnrollementCreatedIntegrationEventConsumer(
        IIdentityModule identityModule,
        ITenantProvider tenantProvider,
        ILogger<EnrollementCreatedIntegrationEventConsumer> logger)
    {
        _identityModule = identityModule;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EnrollementCreatedIntegrationEvent> context)
    {
        var msg = context.Message;
        var tenantId = msg.TenantId != Guid.Empty ? msg.TenantId : (_tenantProvider.GetTenant() ?? Guid.Empty);

        var courseArn = ResourceArn.ForCourse(tenantId, msg.CourseId).ToString();

        var command = new AssignPrincipalToUserCommand(
            msg.StudentId,
            StudentPrincipalTemplateId,
            courseArn);

        var result = await _identityModule.Send(command, context.CancellationToken);

        if (result.IsError)
        {
            _logger.LogError(
                "Failed to assign Student principal to User {UserId} for Course {CourseArn}. Error: {Errors}",
                msg.StudentId, courseArn, string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        _logger.LogInformation(
            "Assigned Student principal {PrincipalId} to User {UserId} for Course {CourseArn}.",
            StudentPrincipalTemplateId, msg.StudentId, courseArn);
    }
}
