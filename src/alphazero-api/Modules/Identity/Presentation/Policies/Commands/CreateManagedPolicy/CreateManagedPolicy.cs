using AlphaZero.Modules.Identity.Application.Policies.Commands.CreateManagedPolicy;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Authorization;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Policies.Commands.CreateManagedPolicy;

public record CreateManagedPolicyRequest
{
    public string Name { get; init; } = default!;
    public List<ManagedPolicyStatement> Statements { get; init; } = new();
}

public record CreateManagedPolicyResponse(Guid Id);

public class CreateManagedPolicySummary : Summary<CreateManagedPolicyEndpoint>
{
    public CreateManagedPolicySummary()
    {
        Summary = "Creates a managed policy";
        Description = "Creates a reusable IAM policy with permission statements.";
        Response<CreateManagedPolicyResponse>(201, "Managed policy created successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Name empty/too long, Statements empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing identity:ManagePolicies permission)");
    }
}

public class CreateManagedPolicyEndpoint : Endpoint<CreateManagedPolicyRequest, CreateManagedPolicyResponse>
{
    private readonly IdentityModule _module;

    public CreateManagedPolicyEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/identity/policies/managed");
        this.AccessControl("identity:ManagePolicies",_ => ResourceArn.AppUrn);
        Description(d => d.WithTags("Identity Policies"));
        Summary(new CreateManagedPolicySummary());
    }

    public override async Task HandleAsync(CreateManagedPolicyRequest req, CancellationToken ct)
    {
        var command = new CreateManagedPolicyCommand(req.Name, req.Statements);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"/identity/policies/managed/{result.Value}", responseBody: new CreateManagedPolicyResponse(result.Value), cancellation: ct);
    }
}
