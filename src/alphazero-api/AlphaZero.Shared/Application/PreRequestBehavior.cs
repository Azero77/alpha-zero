using System.Reflection;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Shared.Application;

public class PreRequestBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IErrorOr
{
    private readonly IEnumerable<IPreRequestHandler<TRequest>> _preRequestHandlers;
    private readonly IAuthorizationContextProvider _authorizationContextProvider;

    public PreRequestBehavior(
        IEnumerable<IPreRequestHandler<TRequest>> preRequestHandlers,
        IAuthorizationContextProvider authorizationContextProvider)
    {
        _preRequestHandlers = preRequestHandlers;
        _authorizationContextProvider = authorizationContextProvider;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        
        if (!_preRequestHandlers.Any())
        {
            return await next();
        }

        var authContext = _authorizationContextProvider.Get(cancellationToken);
        if (authContext.IsError)
            return (dynamic)authContext.Errors;
        foreach (var handler in _preRequestHandlers)
        {
            var result = await handler.PreHandle(request, authContext.Value, cancellationToken);
            if (result.IsError)
            {
                return (dynamic)result.Errors;
            }
        }

        return await next();
    }
}

public static class PreRequestExtensions
{
    public static void AddPreRequestHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        services.Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IPreRequestHandler<>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());
    }
}
