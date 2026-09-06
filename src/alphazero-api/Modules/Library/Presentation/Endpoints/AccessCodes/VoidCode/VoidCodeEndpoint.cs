using AlphaZero.Modules.Library.Application.AccessCodes.VoidCode;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.VoidCode;

public record VoidCodeRequest
{
    public string RawCode { get; init; } = default!;
    public string Reason { get; init; } = default!;
}

public class VoidCodeSummary : Summary<VoidCodeEndpoint>
{
    public VoidCodeSummary()
    {
        Summary = "Voids an access code";
        Description = "Permanently voids an access code so it cannot be redeemed.";
        Response(204, "Access code voided successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (RawCode or Reason empty/too long)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Access code not found (AccessCode.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Access code already voided (AccessCode.AlreadyVoided)");
    }
}

public class VoidCodeEndpoint(LibraryModule module) : Endpoint<VoidCodeRequest>
{
    public override void Configure()
    {
        Post("/library/access-codes/void");
        // Voiding codes is an administrative task
        this.AccessControl("library:Audit", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Library"));
        Summary(new VoidCodeSummary());
    }

    public override async Task HandleAsync(VoidCodeRequest req, CancellationToken ct)
    {
        var command = new VoidCodeCommand(req.RawCode, req.Reason);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
