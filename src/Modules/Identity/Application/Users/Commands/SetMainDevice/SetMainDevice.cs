using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Identity.Application.Users.Commands.SetMainDevice;

public record SetMainDeviceCommand(
    Guid TenantUserId,
    Guid DeviceId) : ICommand<Success>;

public class SetMainDeviceCommandHandler(
    IRepository<TenantUser> userRepository,
    IClock clock) : IRequestHandler<SetMainDeviceCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(SetMainDeviceCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetById(request.TenantUserId);
        if (user is null) return Error.NotFound("User.NotFound");

        var result = user.SetMainDevice(request.DeviceId, clock.Now);
        if (result.IsError) return result.Errors;

        return Result.Success;
    }
}
