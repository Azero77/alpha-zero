using AlphaZero.Modules.Library.Application.RedeemCode;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.RedeemCode;

public record RedeemCodeRequest
{
    public string RawCode { get; init; } = default!;
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
        Description(d => d.WithTags("Library"));
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

        await Send.OkAsync(ct);
    }
}
