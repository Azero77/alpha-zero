using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.CreatePrincipal;

public record CreatePrincipalCommand(
    string Username, 
    string Password,
    string PrincipalType, 
    string? PrincipalScope, 
    string Name) : ICommand<Guid>;

public class CreatePrincipalCommandValidator : AbstractValidator<CreatePrincipalCommand>
{
    public CreatePrincipalCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PrincipalType)
            .NotEmpty()
            .IsEnumName(typeof(PrincipalType), caseSensitive: false)
            .WithMessage("Invalid principal type.");
    }
}

public sealed class CreatePrincipalCommandHandler : IRequestHandler<CreatePrincipalCommand, ErrorOr<Guid>>
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<CreatePrincipalCommandHandler> _logger;

    public CreatePrincipalCommandHandler(
        IPrincipalRepository principalRepository,
        ITenantProvider tenantProvider,
        IPasswordHasher passwordHasher,
        ILogger<CreatePrincipalCommandHandler> logger)
    {
        _principalRepository = principalRepository;
        _tenantProvider = tenantProvider;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(CreatePrincipalCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        if (await _principalRepository.Any(p => p.Username == request.Username && p.TenantId == tenantId.Value, cancellationToken))
        {
            return Error.Conflict("Principal.DuplicateUsername", $"A principal with the username '{request.Username}' already exists in this tenant.");
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var principalType = Enum.Parse<PrincipalType>(request.PrincipalType, ignoreCase: true);

        var principalId = Guid.NewGuid();
        var principalResult = Principal.Create(
            principalId, 
            request.Username, 
            passwordHash, 
            request.Name, 
            principalType, 
            request.PrincipalScope, 
            tenantId.Value);

        if (principalResult.IsError) return principalResult.Errors;

        _principalRepository.Add(principalResult.Value);
        _logger.LogInformation("Principal {PrincipalId} (Username: {Username}) created in Tenant {TenantId}.", 
            principalId, request.Username, tenantId.Value);

        return principalId;
    }
}
