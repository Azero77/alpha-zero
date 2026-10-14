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
    private readonly IAuthorizationContextProvider _authorizationContextFactory;

    public PreRequestBehavior(
        IEnumerable<IPreRequestHandler<TRequest>> preRequestHandlers,
        IAuthorizationContextProvider authorizationContextFactory)
    {
        _preRequestHandlers = preRequestHandlers;
        _authorizationContextFactory = authorizationContextFactory;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        
        if (!_preRequestHandlers.Any())
        {
            return await next();
        }

        var authContext = _authorizationContextFactory.CurrentAuthorizationContext;
        if (authContext == null)
        {
            var contextResult = await _authorizationContextFactory.GetOrCreateCurrentContextAsync(token: cancellationToken);
            if (!contextResult.IsError)
            {
                authContext = contextResult.Value;
            }
        }

        foreach (var handler in _preRequestHandlers)
        {
            var result = await handler.PreHandle(request, authContext, cancellationToken);
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
