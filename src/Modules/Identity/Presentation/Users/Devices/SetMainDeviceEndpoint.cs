using AlphaZero.Modules.Identity.Application.Users.Commands.SetMainDevice;
using AlphaZero.Shared;
using AlphaZero.Shared.Presentation;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.Identity.Presentation.Users.Devices;

public class SetMainDeviceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("identity/users/devices/main", async (SetMainDeviceRequest request, ISender sender, HttpContext httpContext) =>
        {
            var userId = httpContext.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            if (userId == null) return Results.Unauthorized();

            var command = new SetMainDeviceCommand(
                Guid.Parse(userId),
                request.DeviceId);

            var result = await sender.Send(command);

            return result.Match(
                _ => Results.Ok(),
                errors => Results.Problem(errors.First().Description));
        })
        .WithName("SetMainDevice")
        .WithTags("Devices")
        .RequireAuthorization();
    }
}

public record SetMainDeviceRequest(Guid DeviceId);
