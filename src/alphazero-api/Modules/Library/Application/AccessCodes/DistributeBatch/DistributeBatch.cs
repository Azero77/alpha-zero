using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.AccessCodes.DistributeBatch;

public record DistributeBatchCommand(Guid BatchId) : ICommand<Success>, ITransactionalCommand;

public class DistributeBatchCommandValidator : AbstractValidator<DistributeBatchCommand>
{
    public DistributeBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
    }
}

public sealed class DistributeBatchCommandHandler(
    IAccessCodeRepository repository,
    ITenantProvider tenantProvider,
    ILogger<DistributeBatchCommandHandler> logger,
    ICurrentTenantUserRepository? currentUserRepository = null) : IRequestHandler<DistributeBatchCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DistributeBatchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetTenant();
        if (tenantId is null)
        {
            return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");
        }

        // Lock ordering discipline (prevents deadlocks between concurrent flows):
        //  1. AccessCode rows ordered deterministically byId (via SELECT ... FOR UPDATE)
        // 2. RedemptionAuditLog row (INSERT)
        // 3. Audit stream projection (P3b)
        // The tenant predicate is part of the locking SQL (I-TENANT): a foreign batch is never
        // locked, read, or distributeable — it reads back as not found.
        var codes = await repository.GetBatchForUpdateAsync(tenantId.Value, request.BatchId, cancellationToken);

        if (codes.Count == 0)
        {
            var batchExists = await repository.BatchExistsAsync(request.BatchId, cancellationToken);
            if (!batchExists)
            {
                return Error.NotFound("Batch.NotFound", $"Access code batch '{request.BatchId}' does not exist.");
            }

            return Error.Conflict(
                "AccessCode.InvalidStatus",
                $"No codes in batch '{request.BatchId}' are in Minted status, so none were distributed.");
        }

        var mintedCodes = codes.Where(c => c.Status == AccessCodeStatus.Minted).ToList();
        if (mintedCodes.Count == 0)
        {
            return Error.Conflict(
                "AccessCode.InvalidStatus",
                $"No codes in batch '{request.BatchId}' are in Minted status, so none were distributed.");
        }

        var currentUser = currentUserRepository != null ? await currentUserRepository.GetCurrentUser() : null;
        foreach (var code in mintedCodes)
        {
            var distributeResult = code.Distribute(currentUser?.UserId);
            if (distributeResult.IsError)
            {
                return distributeResult.Errors;
            }
        }

        var rowsAffected = await repository.UpdateBatch(tenantId.Value, request.BatchId, AccessCodeStatus.Minted, AccessCodeStatus.Distributed, cancellationToken);

        logger.LogInformation("Distributed {Count} codes in Batch {BatchId}.", rowsAffected, request.BatchId);

        return Result.Success;
    }
}
