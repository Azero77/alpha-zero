using AlphaZero.Modules.Library.Application.RedeemCode;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;

public record RedeemCodeRequest
{
    public string RawCode { get; init; } = default!;
}

public class RedeemCodeSummary : Summary<RedeemCodeEndpoint>
{
    public RedeemCodeSummary()
    {
        Summary = "Redeems an access code";
        Description = "Enrolls the authenticated user into the course/resource associated with the access code.";
        Response(200, "Access code redeemed successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (User must be logged in)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (AccessCode.TenantMismatch or missing courses:Enroll permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Access code not found (AccessCode.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Invalid code status (AccessCode.InvalidStatus)");
    }
}

public class RedeemCodeEndpoint : Endpoint<RedeemCodeRequest>
{
    private readonly LibraryModule _module;

    public RedeemCodeEndpoint(LibraryModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/library/redeem");
        // Redemption requires permission to enroll in courses at the tenant level
        this.AccessControl("courses:Enroll", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Library"));
        Summary(new RedeemCodeSummary());
    }

    public override async Task HandleAsync(RedeemCodeRequest req, CancellationToken ct)
    {
        var command = new RedeemCodeCommand(req.RawCode);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(cancellation: ct);
    }
}
