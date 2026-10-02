using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Tenants.Application.Tenants.Commands.DeleteTenant;

public record DeleteTenantCommand(Guid Id) : ICommand<Success>;

public sealed class DeleteTenantCommandHandler(
    ITenantRepository tenantRepository,
    ILogger<DeleteTenantCommandHandler> logger) : IRequestHandler<DeleteTenantCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetById(request.Id);
        if (tenant is null) return Error.NotFound("Tenant.NotFound", "Tenant not found.");

        tenantRepository.Remove(tenant);
        
        logger.LogWarning("Tenant {TenantId} has been deleted.", tenant.Id);

        return Result.Success;
    }
}
