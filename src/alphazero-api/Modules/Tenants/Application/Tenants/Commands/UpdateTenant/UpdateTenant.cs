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
    string? PrimaryColor,
    string? SecondaryColor,
    string? LogoUrl,
    string? DarkModeLogoUrl,
    string? FaviconUrl) : ICommand<Success>;

public class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);

        RuleFor(x => x.PrimaryColor)
            .Matches(@"^#([0-9A-Fa-f]{6})$")
            .When(x => !string.IsNullOrWhiteSpace(x.PrimaryColor))
            .WithMessage("Primary color must be a valid 6-character hex color (e.g. #2563eb).");

        RuleFor(x => x.SecondaryColor)
            .Matches(@"^#([0-9A-Fa-f]{6})$")
            .When(x => !string.IsNullOrWhiteSpace(x.SecondaryColor))
            .WithMessage("Secondary color must be a valid 6-character hex color (e.g. #2563eb).");

        RuleFor(x => x.LogoUrl)
            .MaximumLength(512)
            .IsValidUrl();

        RuleFor(x => x.DarkModeLogoUrl)
            .MaximumLength(512)
            .IsValidUrl();

        RuleFor(x => x.FaviconUrl)
            .MaximumLength(512)
            .IsValidUrl();
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

        tenant.UpdateDetails(request.Name);
        var updateResult = tenant.UpdateBranding(brandingResult.Value);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        tenantRepository.Update(tenant);
        
        logger.LogInformation("Tenant {TenantId} branding and details updated.", tenant.Id);

        return Result.Success;
    }
}
