using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Middleware.Outbox;
using Microsoft.Extensions.Options;

namespace AlphaZero.Shared.Infrastructure;

/// <summary>
/// A thread-safe implementation of IBusOutboxNotification to replace MassTransit's buggy default implementation 
/// when multiple DbContexts use the outbox on the same bus, preventing NullReferenceExceptions.
/// </summary>
public class ThreadSafeBusOutboxNotification : IBusOutboxNotification
{
    private readonly object _lock = new object();
    private readonly IOptions<OutboxDeliveryServiceOptions> _options;
    private readonly HashSet<CancellationTokenSource> _sources = new HashSet<CancellationTokenSource>();

    public ThreadSafeBusOutboxNotification(IOptions<OutboxDeliveryServiceOptions> options)
    {
        _options = options;
    }

    public async Task WaitForDelivery(CancellationToken cancellationToken)
    {
        CancellationTokenSource cts;
        lock (_lock)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _sources.Add(cts);
        }

        try
        {
            var delay = await Task.Delay(_options.Value.QueryDelay, cts.Token)
                .ContinueWith(t => t, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
                .ConfigureAwait(false);

            if (delay.IsCanceled)
                cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            lock (_lock)
            {
                _sources.Remove(cts);
                cts.Dispose();
            }
        }
    }

    public void Delivered()
    {
        lock (_lock)
        {
            foreach (var cts in _sources)
            {
                try
                {
                    cts.Cancel();
                }
                catch
                {
                    // Ignore cancellation exceptions
                }
            }
            _sources.Clear();
        }
    }
}
