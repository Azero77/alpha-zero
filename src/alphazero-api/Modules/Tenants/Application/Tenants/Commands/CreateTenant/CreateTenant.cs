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
    string? SecondaryColor = null,
    string? DarkModeLogoUrl = null,
    string? FaviconUrl = null) : ICommand<Guid>;

public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Subdomain).NotEmpty().MaximumLength(64).Matches("^[a-z0-9-]+$")
            .WithMessage("Subdomain can only contain lowercase letters, numbers, and hyphens.");

        RuleFor(x => x.PrimaryColor)
            .Matches(@"^#([0-9A-Fa-f]{6})$")
            .When(x => !string.IsNullOrWhiteSpace(x.PrimaryColor))
            .WithMessage("Primary color must be a valid 6-character hex color (e.g. #2563eb).");

        RuleFor(x => x.SecondaryColor)
            .Matches(@"^#([0-9A-Fa-f]{6})$")
            .When(x => !string.IsNullOrWhiteSpace(x.SecondaryColor))
            .WithMessage("Secondary color must be a valid 6-character hex color (e.g. #2563eb).");

        RuleFor(x => x.LogoUrl).MaximumLength(512).IsValidUrl();
        RuleFor(x => x.DarkModeLogoUrl).MaximumLength(512).IsValidUrl();
        RuleFor(x => x.FaviconUrl).MaximumLength(512).IsValidUrl();
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

        var brandingResult = TenantBranding.Create(
            request.PrimaryColor,
            request.SecondaryColor,
            request.LogoUrl,
            request.DarkModeLogoUrl,
            request.FaviconUrl);

        if (brandingResult.IsError)
        {
            return brandingResult.Errors;
        }

        var tenant = Tenant.Create(
            request.Name,
            request.Subdomain,
            brandingResult.Value);

        tenantRepository.Add(tenant);
        
        logger.LogInformation("New tenant created: {TenantName} ({Subdomain}) with ID {TenantId}.", 
            tenant.Name, tenant.Subdomain, tenant.Id);

        return tenant.Id;
    }
}
