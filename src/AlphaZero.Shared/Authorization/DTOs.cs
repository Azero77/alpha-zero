using AlphaZero.Shared.Domain;
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
}

public class IAMPreprocessor : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        //The request may contain TenantUser information, or principal information
        // the principal will have a claim called auth_method = principal wherease the tenant user will auth_method = tenantUser
        var requirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<AccessControlRequirement>();

        if (requirement is null) return;

        var id = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var auth_scheme = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "auth_method")?.Value;

        if (auth_scheme is null || Enum.TryParse<AuthorizationMethod>(auth_scheme, out AuthorizationMethod authMethod))
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);return;
        }

        
        if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out _))
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);return;
        }
            
        var evaluator = context.HttpContext.RequestServices.GetRequiredService<IPolicyEvaluatorService>();

        ResourceArn resourceArn = requirement.resourceArnFactory(context.Request!);
        if (!Guid.TryParse(resourceArn.TenantIdString, out var tenantId))
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);return;
        }
        if(Enum.TryParse<ResourceType>(resourceArn.Service, out var resourceType))
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);return;
        }
        var result = await evaluator.Authorize(Guid.Parse(id), tenantId, resourceArn.ResourcePath, resourceType, requirement.Action);

        if (result.IsError)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);return;
        }
            
    }
}

public enum AuthorizationMethod
{
    Principal,
    TenantUser
}
