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
        var count = await repository.Entities.Where(x => x.BatchId == request.BatchId && x.Status == AccessCodeStatus.Minted)
            .ExecuteUpdateAsync(setter => setter.SetProperty(s => s.Status,AccessCodeStatus.Distributed));

        logger.LogInformation("Distributed {Count} codes in Batch {BatchId}.", count, request.BatchId);

        return Result.Success;
    }
}
