using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace AlphaZero.Modules.Identity.Application.Users.Commands.RegisterDevice;

public record RegisterDeviceCommand(
    Guid TenantUserId,
    string DeviceName,
    DevicePlatform Platform,
    string PublicKey) : ICommand<Guid>;

public class RegisterDeviceCommandHandler(
    IRepository<TenantUser> userRepository,
    IClock clock,
    IPublicKeyProvider publicKeyProvider) : IRequestHandler<RegisterDeviceCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetById(request.TenantUserId);
        if (user is null) return Error.NotFound("User.NotFound");

        var result = user.RegisterDevice(request.DeviceName, request.Platform, request.PublicKey, clock.Now);
        if (result.IsError) return result.Errors;

        var device = user.Devices.Last(); // Newly added device

        //evict cache
        await publicKeyProvider.SetNewDevicePublicKey(request.TenantUserId.ToString(), device.Id.ToString(), device.PublicKey, cancellationToken);
        return device.Id;
    }
}
