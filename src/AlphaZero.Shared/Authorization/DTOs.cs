using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaZero.Shared.Authorization;

public record AccessControlRequirement(string Action, Func<object,ResourceArn> resourceArnFactory);


public static class EndpointExtensions
{
    public static void AccessControl<TRequest>(this Endpoint<TRequest> endpoint, string action, Func<TRequest, ResourceArn> resourceArnFactory)
        where TRequest : notnull
    {
        var requirement = new AccessControlRequirement(action, req => resourceArnFactory((TRequest)req));
        endpoint.Definition.Metadata(requirement);
    }
    public static void AccessControl<TRequest, TResponse>(this Endpoint<TRequest, TResponse> endpoint, string action, Func<TRequest, ResourceArn> resourceArnFactory)
        where TRequest : notnull
    {
        var requirement = new AccessControlRequirement(action, req => resourceArnFactory((TRequest)req));
        endpoint.Definition.Metadata(requirement);
    }

    public static RouteHandlerBuilder AccessControl(this RouteHandlerBuilder builder, string action, Func<HttpContext, ResourceArn> resourceArnFactory)
    {
        var requirement = new AccessControlRequirement(action, req => resourceArnFactory((HttpContext)req));
        return builder.WithMetadata(requirement);
    }
}

public class IAMPreprocessor : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var requirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<AccessControlRequirement>();

        if (requirement is null) return;

        var id = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var auth_scheme = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "auth_method")?.Value;
        var sid = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "sid")?.Value;

        if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out var principalId))
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct); return;
        }

        if (string.IsNullOrEmpty(auth_scheme))
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct); return;
        }

        Guid? sessionId = Guid.TryParse(sid, out var sessionGuid) ? sessionGuid : null;
            
        var evaluator = context.HttpContext.RequestServices.GetRequiredService<IPolicyEvaluatorService>();
        var tenantProvider = context.HttpContext.RequestServices.GetRequiredService<ITenantProvider>();

        ResourceArn resourceArn = requirement.resourceArnFactory(context.Request!);
        Guid tenantId;

        // If the ARN uses Guid.Empty placeholder, resolve it from the tenant provider
        if (resourceArn.TenantIdString == Guid.Empty.ToString())
        {
            var currentTenant = tenantProvider.GetTenant();
            if (currentTenant == null)
            {
                await context.HttpContext.Response.SendForbiddenAsync(ct); return;
            }
            tenantId = currentTenant.Value;
        }
        else if (!Guid.TryParse(resourceArn.TenantIdString, out tenantId) && resourceArn.TenantIdString != ResourceArn.GlobalTenant)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct); return;
        }

        if (!Enum.TryParse<ResourceType>(resourceArn.Service, true, out var resourceType))
        {
            // If service name doesn't match ResourceType enum, we might need a fallback or stricter validation
            await context.HttpContext.Response.SendForbiddenAsync(ct); return;
        }

        var result = await evaluator.Authorize(
            principalId, 
            tenantId, 
            resourceArn.ResourcePath, 
            resourceType, 
            requirement.Action, 
            auth_scheme, 
            sessionId);

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

public enum AuthorizationMethod
{
    Principal,
    TenantUser
}
