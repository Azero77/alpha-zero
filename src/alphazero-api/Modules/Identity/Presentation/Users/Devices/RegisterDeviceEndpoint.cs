using AlphaZero.Modules.Identity.Application.Users.Commands.RegisterDevice;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Users.Devices;

public record RegisterDeviceRequest
{
    public string DeviceName { get; init; } = default!;
    public string Platform { get; init; } = default!;
    public string PublicKey { get; init; } = default!;
}

public record RegisterDeviceResponse(Guid DeviceId);

public class RegisterDeviceSummary : Summary<RegisterDeviceEndpoint>
{
    public RegisterDeviceSummary()
    {
        Summary = "Registers a new device for the user";
        Description = "Registers a client device public key for secure offline playback.";
        Response<RegisterDeviceResponse>(200, "Device registered successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (DeviceName empty/too long, PublicKey empty, Platform invalid)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Device already registered (Device.Exists)");
    }
}

public class RegisterDeviceEndpoint(IdentityModule module) : Endpoint<RegisterDeviceRequest, RegisterDeviceResponse>
{
    public override void Configure()
    {
        Post("/identity/users/devices");
        Description(d => d.WithTags("Devices"));
        Summary(new RegisterDeviceSummary());
    }

    public override async Task HandleAsync(RegisterDeviceRequest req, CancellationToken ct)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var command = new RegisterDeviceCommand(
            Guid.Parse(userId),
            req.DeviceName,
            req.Platform,
            req.PublicKey);

        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new RegisterDeviceResponse(result.Value), ct);
    }
}
