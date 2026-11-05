using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.AccessCodes.VoidCode;

public record VoidCodeCommand(string RawCode, string Reason) : ICommand<Success>, ITransactionalCommand;

public class VoidCodeCommandValidator : AbstractValidator<VoidCodeCommand>
{
    public VoidCodeCommandValidator()
    {
        RuleFor(x => x.RawCode).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}

public sealed class VoidCodeCommandHandler(
    IAccessCodeRepository repository,
    IRedemptionStrategyFactory strategyFactory,
    IPasswordHasher passwordHasher,
    ITenantProvider tenantProvider,
    ILogger<VoidCodeCommandHandler> logger) : IRequestHandler<VoidCodeCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(VoidCodeCommand request, CancellationToken cancellationToken)
    {
        // Lock ordering discipline (prevents deadlocks between concurrent redeem/void flows):
        // 1. AccessCode row (via SELECT ... FOR UPDATE)
        // 2. RedemptionAuditLog row (INSERT)
        // 3. Audit stream projection (P3b)
        var hash = passwordHasher.HashPassword(request.RawCode);
        var code = await repository.GetByHashForUpdateAsync(hash, cancellationToken);

        if (code is null)
        {
            return Error.NotFound("AccessCode.NotFound", "The provided code is invalid.");
        }

        var currentTenantId = tenantProvider.GetTenant();
        if (code.TenantId != currentTenantId)
        {
            return Error.Forbidden("AccessCode.TenantMismatch", "This code belongs to another academy.");
        }

        var wasRedeemed = code.Status == AccessCodeStatus.Redeemed;
        var userId = code.RedeemedByUserId;

        var result = code.Void(request.Reason);
        if (result.IsError) return result.Errors;

        // If it was already redeemed, trigger the revocation strategy
        if (wasRedeemed && userId.HasValue)
        {
            var strategy = strategyFactory.GetRevocationStrategy(code.StrategyId);
            await strategy.ExecuteAsync(userId.Value, code.Id, code.TargetResourceArn);
            logger.LogInformation("Triggered revocation strategy '{StrategyId}' for User {UserId}.", code.StrategyId, userId.Value);
        }

        var updateResult = await repository.UpdateAsync(code, cancellationToken);
        if (updateResult.IsError) return updateResult.Errors;

        logger.LogWarning("Access Code {CodeId} has been VOIDED. Reason: {Reason}", code.Id, request.Reason);

        return Result.Success;
    }
}
