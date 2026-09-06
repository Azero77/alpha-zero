using AlphaZero.Modules.Identity.Application.Principals.Queries.GetPrincipalPolicies;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Principals.Queries.GetPrincipalPolicies;

public record GetPrincipalPoliciesRequest { public Guid PrincipalId { get; init; } }

public class GetPrincipalPoliciesSummary : Summary<GetPrincipalPoliciesEndpoint>
{
    public GetPrincipalPoliciesSummary()
    {
        Summary = "Gets policies for a principal";
        Description = "Retrieves both inline and managed policies associated with the specified principal.";
        Response<PrincipalPoliciesDto>(200, "Principal policies retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing identity:ManagePrincipals permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Principal not found (Principal.NotFound)");
    }
}

public class GetPrincipalPoliciesEndpoint : Endpoint<GetPrincipalPoliciesRequest, PrincipalPoliciesDto>
{
    private readonly IdentityModule _module;

    public GetPrincipalPoliciesEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/identity/principals/{PrincipalId}/policies");
        this.AccessControl("identity:ManagePrincipals", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Identity"));
        Summary(new GetPrincipalPoliciesSummary());
    }

    public override async Task HandleAsync(GetPrincipalPoliciesRequest req, CancellationToken ct)
    {
        var query = new GetPrincipalPoliciesQuery(req.PrincipalId);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
