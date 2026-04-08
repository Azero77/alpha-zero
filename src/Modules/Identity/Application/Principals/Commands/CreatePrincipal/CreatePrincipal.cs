using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.CreatePrincipal;

public record CreatePrincipalCommand(
    string IdentityId, 
    PrincipalType PrincipalType, 
    string PrincipalScope, 
    string Name,
    Guid? ResourceId = null,
    ResourceType? ScopeResourceType = null) : ICommand<Guid>;

public class CreatePrincipalCommandValidator : AbstractValidator<CreatePrincipalCommand>
{
    public CreatePrincipalCommandValidator()
    {
        RuleFor(x => x.IdentityId).NotEmpty();
        RuleFor(x => x.PrincipalScope).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreatePrincipalCommandHandler : IRequestHandler<CreatePrincipalCommand, ErrorOr<Guid>>
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CreatePrincipalCommandHandler> _logger;

    public CreatePrincipalCommandHandler(
        IPrincipalRepository principalRepository,
        ITenantProvider tenantProvider,
        ILogger<CreatePrincipalCommandHandler> logger)
    {
        _principalRepository = principalRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(CreatePrincipalCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var principalId = Guid.NewGuid();
        var principalResult = Principal.Create(
            principalId, 
            request.IdentityId, 
            request.PrincipalType, 
            tenantId.Value, 
            request.PrincipalScope, 
            request.Name,
            request.ResourceId,
            request.ScopeResourceType);

        if (principalResult.IsError) return principalResult.Errors;

        _principalRepository.Add(principalResult.Value);
        _logger.LogInformation("Principal {PrincipalId} created for Identity {IdentityId} in Tenant {TenantId}.", 
            principalId, request.IdentityId, tenantId.Value);

        return principalId;
    }
}
