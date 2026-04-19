using AlphaZero.Modules.Library.Application.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Library.Application.AccessCodes.GenerateAdminCode;

public record GenerateAdminCodeCommand(
    string TargetResourceArn,
    string StrategyId = "enroll-course",
    Dictionary<string, object>? Metadata = null) : ICommand<string>;

public sealed class GenerateAdminCodeCommandHandler(
    IAccessCodeRepository accessCodeRepository,
    ITenantProvider tenantProvider,
    IPasswordHasher passwordHasher,
    ILogger<GenerateAdminCodeCommandHandler> logger) : IRequestHandler<GenerateAdminCodeCommand, ErrorOr<string>>
{
    public async Task<ErrorOr<string>> Handle(GenerateAdminCodeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var resourceArn = ResourceArn.Create(request.TargetResourceArn);
        if (resourceArn.IsError) return resourceArn.Errors;

        // Admin codes have null LibraryId and a specific prefix
        var rawCode = "ADM-" + CodeGeneratorHelper.GenerateFriendlyCode();
        var hash = passwordHasher.HashPassword(rawCode);
        
        var metadata = request.Metadata ?? new Dictionary<string, object>();
        metadata["GeneratedBy"] = "Administrator";

        var accessCode = AccessCode.Mint(
            hash,
            tenantId.Value,
            null, // No Library bound
            request.StrategyId,
            resourceArn.Value,
            JsonDocument.Parse(JsonSerializer.Serialize(metadata)));

        accessCodeRepository.Add(accessCode);
        
        logger.LogInformation("Administrator generated a direct access code for {Target} in Tenant {TenantId}.",
            request.TargetResourceArn, tenantId.Value);

        return rawCode;
    }
}
