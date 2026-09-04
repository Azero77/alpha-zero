using AlphaZero.Modules.Identity.Application.Users.Commands.SetMainDevice;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Users.Devices;

public record SetMainDeviceRequest
{
    public Guid DeviceId { get; init; }
}

public class SetMainDeviceEndpoint(IdentityModule module) : Endpoint<SetMainDeviceRequest>
{
    public override void Configure()
    {
        Post("/identity/users/devices/main");
        Description(d => d.WithTags("Devices"));
    }

    public override async Task HandleAsync(SetMainDeviceRequest req, CancellationToken ct)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var command = new SetMainDeviceCommand(
            Guid.Parse(userId),
            req.DeviceId);

        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
