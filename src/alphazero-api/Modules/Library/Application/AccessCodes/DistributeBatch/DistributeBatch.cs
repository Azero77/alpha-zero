using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.AccessCodes.DistributeBatch;

public record DistributeBatchCommand(Guid BatchId) : ICommand<Success>;

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
        var count = await repository.MarkBatchAsDistributedAsync(request.BatchId, cancellationToken);

        if (count == 0)
        {
            // A zero-row bulk update is never a success. Distinguish the two causes:
            // a nonexistent batch is a not-found error; a batch with no Minted codes
            // left is a status conflict (the 409 conformance path per spec).
            var batchExists = await repository.BatchExistsAsync(request.BatchId, cancellationToken);

            if (!batchExists)
            {
                return Error.NotFound("Batch.NotFound", $"Access code batch '{request.BatchId}' does not exist.");
            }

            return Error.Conflict(
                "AccessCode.InvalidStatus",
                $"No codes in batch '{request.BatchId}' are in Minted status, so none were distributed.");
        }

        logger.LogInformation("Distributed {Count} codes in Batch {BatchId}.", count, request.BatchId);

        return Result.Success;
    }
}
