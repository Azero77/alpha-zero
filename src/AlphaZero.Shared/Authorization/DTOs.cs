using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.DependencyInjection;

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

public class IAMPreprocessor(IAuthorizationContextFactory authorizationContextFactory, IPolicyEvaluatorService evaluator, ITenantProvider tenantProvider) : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var globalRequirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<GlobalAccessControlRequirement>();
        var tenantScopedRequirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<AccessControlWithTenantRequirement>();
        
        if (globalRequirement is null && tenantScopedRequirement is null) return;

        var id = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var auth_scheme = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "auth_method")?.Value;
        if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out var principalId) || string.IsNullOrEmpty(auth_scheme))
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct); return;
        }

        ResourceArn resourceArn;
        if (globalRequirement != null)
        {
            resourceArn = globalRequirement.resourceArnFactory(context.Request!);
        }
        else
        {
            var currentTenant = tenantProvider.GetTenant();
            if (currentTenant == null)
            {
                await context.HttpContext.Response.SendForbiddenAsync(ct); return;
            }
            resourceArn = tenantScopedRequirement!.resourceArnFactory(context.Request!, currentTenant.Value);
        }
        var authContextResult = await authorizationContextFactory.Create(resourceArn, Enum.Parse<AuthenticationMethod>(auth_scheme, true), id);

        if (authContextResult.IsError)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct); return;
        }

        var authContext = authContextResult.Value with
        {
            RequiredPermission = globalRequirement?.Action ?? tenantScopedRequirement!.Action
        };
        
        var result = await evaluator.Authorize(authContext);

        if (result.IsError)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct); return;
        }
    }
}
public class IAMDevPreprocessor : IGlobalPreProcessor
{
    public Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        return Task.CompletedTask; //this is dev for skipping authorization
    }
}

public enum AuthenticationMethod
{
    Principal,
    TenantUser
}

