using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace AlphaZero.Shared.Authorization;


public delegate ResourceArn GlobalResourceArnResolver(object request);
public delegate ResourceArn TenantScopedResourceArnResolver(object request, Guid TenantId);
public record GlobalAccessControlRequirement(string Action, GlobalResourceArnResolver resourceArnFactory);
public record AccessControlWithTenantRequirement(string Action, TenantScopedResourceArnResolver resourceArnFactory);

public static class EndpointExtensions
{
    public static void AccessControl<TRequest>(this Endpoint<TRequest> endpoint, string action, Func<TRequest, ResourceArn> resourceArnFactory)
        where TRequest : notnull
    {
        var requirement = new GlobalAccessControlRequirement(action, req => resourceArnFactory((TRequest)req));
        endpoint.Definition.Metadata(requirement);

    }

    public static void AccessControl<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, string action, Func<TRequest, ResourceArn> resourceArnFactory)
        where TRequest : notnull
    {
        var requirement = new GlobalAccessControlRequirement(action, req => resourceArnFactory((TRequest)req));
        endpoint.Definition.Metadata(requirement);
    }
    public static void AccessControl<TRequest>(this Endpoint<TRequest> endpoint, string action, Func<TRequest, Guid, ResourceArn> resourceArnFactory)
        where TRequest : notnull
    {
        var requirement = new AccessControlWithTenantRequirement(action, (req, tenantId) => resourceArnFactory((TRequest)req, tenantId));
        endpoint.Definition.Metadata(requirement);
    }

    public static void AccessControl<TRequest, TResponse>(this Endpoint<TRequest,TResponse> endpoint, string action, Func<TRequest, Guid, ResourceArn> resourceArnFactory)
        where TRequest : notnull
    {
        var requirement = new AccessControlWithTenantRequirement(action, (req, tenantId) => resourceArnFactory((TRequest)req, tenantId));
        endpoint.Definition.Metadata(requirement);
    }
    public static RouteHandlerBuilder AccessControl(this RouteHandlerBuilder builder, string action, Func<HttpContext, ResourceArn> resourceArnFactory)
    {
        var requirement = new GlobalAccessControlRequirement(action, req => resourceArnFactory((HttpContext)req));

        return builder.WithMetadata(requirement);
    }

    public static RouteHandlerBuilder AccessControl(this RouteHandlerBuilder builder, string action, Func<HttpContext,Guid, ResourceArn> resourceArnFactory)
    {
        var requirement = new AccessControlWithTenantRequirement(action, (req, tenantId) => resourceArnFactory((HttpContext)req, tenantId));

        return builder.WithMetadata(requirement);
    }
}

public class IAMPreprocessor : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        if (context.Request is not null)
        {
            context.HttpContext.Items["__FastEndpoints_Request__"] = context.Request;
        }

        var authProvider = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationContextProvider>();
        var evaluator = context.HttpContext.RequestServices.GetRequiredService<IPolicyEvaluatorService>();

        var authResult = authProvider.Get(ct);

        if (authResult.IsError)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);
            return;
        }

        var authContext = authResult.Value;
        if (authContext is null || string.IsNullOrEmpty(authContext.RequiredPermission))
        {
            return;
        }

        var result = await evaluator.Authorize(authContext);

        if (result.IsError)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);
            return;
        }
    }
}
public class IAMDevPreprocessor : IGlobalPreProcessor
{
    public Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        // Dev/bypass skips EVALUATION only, not the context build-up: the request-side guards
        // (issue #34 story #10) read the AccessControl envelope (the provider attaches
        // RequiredPermission/ResourcePath from this stashed typed request), so dev stashes
        // exactly like the enforcing preprocessor above. Without this, bypass-mode contexts
        // degrade to "no permission metadata" and every envelope-gated guard would fail-open
        // or lock out the admin path.
        if (context.Request is not null)
        {
            context.HttpContext.Items["__FastEndpoints_Request__"] = context.Request;
        }

        return Task.CompletedTask; // dev for skipping authorization
    }
}

public enum AuthenticationMethod
{
    Principal,
    TenantUser,
    GlobalUser,
    System
}

public record ClaimDTO(string Key, string Value);


