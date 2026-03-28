using MassTransit;
using MassTransit.Mediator;

namespace AlphaZero.Shared.Application;

/// <summary>
/// A unified cross-module communication bus. 
/// Handles Commands (Send), Events (Publish), and Queries (GetResponse) in-memory.
/// </summary>
public interface IModuleBus
{
    Task Publish<T>(T message, CancellationToken cancellationToken = default)
       where T : class;

    Task Send<T>(T message, CancellationToken cancellationToken = default)
        where T : class;

    Task<TResponse> GetResponse<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;
}

public class ModuleBus : IModuleBus
{
    private readonly IMediator _mediator;

    public ModuleBus(IMediator mediator)
    {
        _mediator = mediator;
    }

    public Task Publish<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        return _mediator.Publish(message, cancellationToken);
    }

    public Task Send<T>(T message, CancellationToken cancellationToken = default)
        where T : class
    {
        return _mediator.Send(message, cancellationToken);
    }

    public async Task<TResponse> GetResponse<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        var client = _mediator.CreateRequestClient<TRequest>();
        var response = await client.GetResponse<TResponse>(request, cancellationToken);
        return response.Message;
    }
}
