using ErrorOr;
using MediatR;

namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Outermost MediatR behavior that flushes queued cache tags only after the UnitOfWork has
/// committed (the UnitOfWork queues them, this behavior applies them). Error results and
/// exceptions discard the queue; a nested dispatch never flushes. Cancellation is never
/// swallowed.
/// </summary>
public sealed class CacheEvictionFlushBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEvictionProvider _provider;
    private readonly EvictionFlushScope _scope;

    public CacheEvictionFlushBehavior(
        IEvictionProvider provider,
        EvictionFlushScope scope)
    {
        _provider = provider;
        _scope = scope;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var _ = _scope.Enter();
        var isOutermost = _scope.IsOutermost;

        if (isOutermost)
            _provider.Begin();

        TResponse response;
        try
        {
            response = await next(cancellationToken);
        }
        catch
        {
            if (isOutermost)
                _provider.Discard();

            throw;
        }

        if (!isOutermost)
            return response;

        if (response is IErrorOr { IsError: true })
        {
            _provider.Discard();
            return response;
        }

        await _provider.FlushAsync(cancellationToken);
        return response;
    }
}
