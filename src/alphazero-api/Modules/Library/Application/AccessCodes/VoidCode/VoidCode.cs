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
    IPasswordHasher passwordHasher,
    ITenantProvider tenantProvider,
    ILogger<VoidCodeCommandHandler> logger,
    ICurrentTenantUserRepository? currentUserRepository = null) : IRequestHandler<VoidCodeCommand, ErrorOr<Success>>
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

        var currentUser = currentUserRepository != null ? await currentUserRepository.GetCurrentUser() : null;
        var result = code.Void(request.Reason, currentUser?.UserId);
        if (result.IsError) return result.Errors;

        var updateResult = await repository.UpdateAsync(code, cancellationToken);
        if (updateResult.IsError) return updateResult.Errors;

        logger.LogWarning("Access Code {CodeId} has been VOIDED. Reason: {Reason}", code.Id, request.Reason);

        return Result.Success;
    }
}
