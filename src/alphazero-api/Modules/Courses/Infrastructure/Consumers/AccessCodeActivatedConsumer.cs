using AlphaZero.Modules.Courses.Application.Enrollments.Commands.ProvisionAccessCode;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

/// <summary>
/// Thin MassTransit adapter over <see cref="ProvisionAccessCodeCommand"/> (issue #58).
/// Resolves the tenant scope and delegates all provisioning logic — ARN/plan resolution,
/// principal minting, enrollment, and FailedProvisioningRecord parking — to the
/// Courses application layer, which owns the business rules.
/// </summary>
public class AccessCodeActivatedConsumer : IConsumer<AccessCodeActivatedIntegrationEvent>
{
    private readonly ICoursesModule _coursesModule;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAmbientTenantContext _ambientTenant;
    private readonly ILogger<AccessCodeActivatedConsumer> _logger;

    public AccessCodeActivatedConsumer(
        ICoursesModule coursesModule,
        ITenantProvider tenantProvider,
        IAmbientTenantContext ambientTenant,
        ILogger<AccessCodeActivatedConsumer> logger)
    {
        _coursesModule = coursesModule;
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

        var command = new ProvisionAccessCodeCommand(
            msg.AccessCodeId,
            msg.RedeemingUserId,
            msg.TargetResourceArn,
            tenantId,
            msg.Metadata);

        var result = await _coursesModule.Send(command, context.CancellationToken);

        if (result.IsError)
        {
            _logger.LogError(
                "Provisioning command failed for AccessCode {CodeId}, User {UserId}. Errors: {Errors}",
                msg.AccessCodeId,
                msg.RedeemingUserId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
