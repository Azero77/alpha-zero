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

public class VoidCodeEndpoint(LibraryModule module) : Endpoint<VoidCodeRequest>
{
    public override void Configure()
    {
        Post("/library/access-codes/void");
        // Voiding codes is an administrative task
        this.AccessControl("library:Audit", _ => ResourceArn.ForTenant(Guid.Empty));
        Description(d => d.WithTags("Library"));
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
