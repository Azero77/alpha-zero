using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Behaviors;

public class TransactionalBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, ErrorOr<TResponse>>
    where TRequest : IRequest<ErrorOr<TResponse>>
{
    private readonly AppDbContext _context;
    private readonly ILogger<TransactionalBehavior<TRequest, TResponse>> _logger;

    public TransactionalBehavior(AppDbContext context, ILogger<TransactionalBehavior<TRequest, TResponse>> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ErrorOr<TResponse>> Handle(
        TRequest request, 
        RequestHandlerDelegate<ErrorOr<TResponse>> next, 
        CancellationToken cancellationToken)
    {
        // Zero overhead for standard commands: pass straight through
        if (request is not ITransactionalCommand)
        {
            return await next(cancellationToken);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await next(cancellationToken);
            if (!result.IsError)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction aborted for {Name}: {Message}", typeof(TRequest).Name, ex.Message);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
