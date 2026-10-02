using AlphaZero.Modules.Identity.Application.Principals.Queries.GetPrincipalsByResource;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Principals.Queries.GetPrincipalsByResource;

public record GetPrincipalsByResourceRequest 
{ 
    public string ResourceType { get; init; } = string.Empty;
    public Guid ResourceId { get; init; } 
}

public class GetPrincipalsByResourceSummary : Summary<GetPrincipalsByResourceEndpoint>
{
    public GetPrincipalsByResourceSummary()
    {
        Summary = "Gets principals assigned to a resource";
        Description = "Retrieves a list of principals that have direct or indirect permissions on the resource.";
        Response<List<PrincipalDto>>(200, "Principals retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing identity:ManagePrincipals permission)");
    }
}

public class GetPrincipalsByResourceEndpoint : Endpoint<GetPrincipalsByResourceRequest, List<PrincipalDto>>
{
    private readonly IdentityModule _module;

    public GetPrincipalsByResourceEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/identity/resources/{ResourceType}/{ResourceId}/principals");
        this.AccessControl("identity:ManagePrincipals", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Identity"));
        Summary(new GetPrincipalsByResourceSummary());
    }

    public override async Task HandleAsync(GetPrincipalsByResourceRequest req, CancellationToken ct)
    {
        var query = new GetPrincipalsByResourceQuery(req.ResourceId, req.ResourceType);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
