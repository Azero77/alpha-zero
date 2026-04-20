using AlphaZero.Modules.Identity.Application.Principals.Queries.GetPrincipalPolicies;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Principals.Queries.GetPrincipalPolicies;

public record GetPrincipalPoliciesRequest { public Guid PrincipalId { get; init; } }

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
        this.AccessControl("identity:ManagePrincipals", _ => ResourceArn.ForTenant(Guid.Empty));
        Description(d => d.WithTags("Identity"));
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
