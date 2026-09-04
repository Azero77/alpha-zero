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

public class IAMPreprocessor(IAuthorizationContextFactory authorizationContextFactory, IPolicyEvaluatorService evaluator, ITenantProvider tenantProvider) : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var globalRequirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<GlobalAccessControlRequirement>();
        var tenantScopedRequirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<AccessControlWithTenantRequirement>();
        
        if (globalRequirement is null && tenantScopedRequirement is null) return;

        var id = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        var auth_scheme = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "auth_method")?.Value ?? "Principal";
        if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out var principalId))
        {
            throw new BadHttpRequestException("Forbidden", StatusCodes.Status403Forbidden);
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
                throw new BadHttpRequestException("Forbidden", StatusCodes.Status403Forbidden);
            }
            resourceArn = tenantScopedRequirement!.resourceArnFactory(context.Request!, currentTenant.Value);
        }
        string? permission = globalRequirement?.Action ?? tenantScopedRequirement?.Action;

        if(permission is null)
        {
            throw new BadHttpRequestException("Forbidden", StatusCodes.Status403Forbidden);
        }
        var authContext = await authorizationContextFactory.Create(permission, resourceArn, Enum.Parse<AuthenticationMethod>(auth_scheme, true), id, ct);

        if (authContext.IsError)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);
            return;
        }
        var result = await evaluator.Authorize(authContext.Value);

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
        return Task.CompletedTask; //this is dev for skipping authorization
    }
}

public enum AuthenticationMethod
{
    Principal,
    TenantUser,
    GlobalUser
}

public record ClaimDTO(string Key, string Value);


