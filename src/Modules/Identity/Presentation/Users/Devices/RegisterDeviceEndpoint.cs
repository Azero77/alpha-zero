using AlphaZero.Modules.Identity.Application.Users.Commands.RegisterDevice;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared;
using AlphaZero.Shared.Presentation;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.Identity.Presentation.Users.Devices;

public class RegisterDeviceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("identity/users/devices", async (RegisterDeviceRequest request, ISender sender, HttpContext httpContext) =>
        {
            var userId = httpContext.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            if (userId == null) return Results.Unauthorized();

            var command = new RegisterDeviceCommand(
                Guid.Parse(userId),
                request.DeviceName,
                request.Platform,
                request.PublicKey);

            var result = await sender.Send(command);

            return result.Match(
                id => Results.Ok(new { DeviceId = id }),
                errors => Results.Problem(errors.First().Description));
        })
        .WithName("RegisterDevice")
        .WithTags("Devices")
        .RequireAuthorization();
    }
}

public record RegisterDeviceRequest(string DeviceName, DevicePlatform Platform, string PublicKey);
