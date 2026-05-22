using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Tenants.Application.Tenants.Commands.CreateTenant;

public record CreateTenantCommand(
    string Name,
    string Subdomain,
    string? LogoUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null) : ICommand<Guid>;

public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Subdomain).NotEmpty().MaximumLength(64).Matches("^[a-z0-9-]+$")
            .WithMessage("Subdomain can only contain lowercase letters, numbers, and hyphens.");
    }
}

public sealed class CreateTenantCommandHandler(
    ITenantRepository tenantRepository,
    ILogger<CreateTenantCommandHandler> logger) : IRequestHandler<CreateTenantCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        if (!await tenantRepository.IsSubdomainUniqueAsync(request.Subdomain, cancellationToken))
        {
            return Error.Conflict("Tenant.SubdomainNotUnique", $"The subdomain '{request.Subdomain}' is already in use.");
        }

        var tenant = Tenant.Create(
            request.Name,
            request.Subdomain,
            request.LogoUrl,
            request.PrimaryColor,
            request.SecondaryColor);

        tenantRepository.Add(tenant);
        
        logger.LogInformation("New tenant created: {TenantName} ({Subdomain}) with ID {TenantId}.", 
            tenant.Name, tenant.Subdomain, tenant.Id);

        return tenant.Id;
    }
}
