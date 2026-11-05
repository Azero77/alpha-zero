using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
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
    ILogger<DistributeBatchCommandHandler> logger) : IRequestHandler<DistributeBatchCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DistributeBatchCommand request, CancellationToken cancellationToken)
    {
        // Lock ordering discipline (prevents deadlocks between concurrent flows):
        // 1. AccessCode rows ordered deterministically by Id (via SELECT ... FOR UPDATE)
        // 2. RedemptionAuditLog row (INSERT)
        // 3. Audit stream projection (P3b)
        var codes = await repository.GetBatchForUpdateAsync(request.BatchId, cancellationToken);

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

        foreach (var code in mintedCodes)
        {
            var distributeResult = code.Distribute();
            if (distributeResult.IsError)
            {
                return distributeResult.Errors;
            }

            var updateResult = await repository.UpdateAsync(code, cancellationToken);
            if (updateResult.IsError)
            {
                return updateResult.Errors;
            }
        }

        logger.LogInformation("Distributed {Count} codes in Batch {BatchId}.", mintedCodes.Count, request.BatchId);

        return Result.Success;
    }
}
