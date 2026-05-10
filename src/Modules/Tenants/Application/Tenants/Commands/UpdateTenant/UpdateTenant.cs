using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Tenants.Application.Tenants.Commands.UpdateTenant;

public record UpdateTenantCommand(
    Guid Id,
    string Name,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor) : ICommand<Success>;

public class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}

public sealed class UpdateTenantCommandHandler(
    ITenantRepository tenantRepository,
    ILogger<UpdateTenantCommandHandler> logger) : IRequestHandler<UpdateTenantCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetById(request.Id);
        if (tenant is null) return Error.NotFound("Tenant.NotFound", "Tenant not found.");

        tenant.UpdateDetails(request.Name, request.LogoUrl);
        tenant.UpdateTheme(request.PrimaryColor, request.SecondaryColor);

        tenantRepository.Update(tenant);
        
        logger.LogInformation("Tenant {TenantId} updated.", tenant.Id);

        return Result.Success;
    }
}
