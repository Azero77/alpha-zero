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

public class RegisterDeviceEndpoint(IdentityModule module) : Endpoint<RegisterDeviceRequest, RegisterDeviceResponse>
{
    public override void Configure()
    {
        Post("/identity/users/devices");
        Description(d => d.WithTags("Devices"));
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
