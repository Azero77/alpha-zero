using AlphaZero.Modules.Identity.Application.Users.Commands.SetMainDevice;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Users.Devices;

public record SetMainDeviceRequest
{
    public Guid DeviceId { get; init; }
}

public class SetMainDeviceSummary : Summary<SetMainDeviceEndpoint>
{
    public SetMainDeviceSummary()
    {
        Summary = "Sets the user's primary device";
        Description = "Updates the user's main device for playback security.";
        Response(204, "Main device updated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Device not found (Device.NotFound)");
    }
}

public class SetMainDeviceEndpoint(IdentityModule module) : Endpoint<SetMainDeviceRequest>
{
    public override void Configure()
    {
        Post("/identity/users/devices/main");
        Description(d => d.WithTags("Devices"));
        Summary(new SetMainDeviceSummary());
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
