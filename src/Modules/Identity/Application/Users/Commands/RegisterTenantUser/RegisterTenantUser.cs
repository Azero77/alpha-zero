using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Users.Commands.RegisterTenantUser;

public record RegisterTenantUserCommand(
    string IdentityId,
    string Name) : ICommand<Guid>;

public class RegisterTenantUserCommandValidator : AbstractValidator<RegisterTenantUserCommand>
{
    public RegisterTenantUserCommandValidator()
    {
        RuleFor(x => x.IdentityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}

public sealed class RegisterTenantUserCommandHandler : IRequestHandler<RegisterTenantUserCommand, ErrorOr<Guid>>
{
    private readonly IRepository<TenantUser> _userRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<RegisterTenantUserCommandHandler> _logger;

    public RegisterTenantUserCommandHandler(
        IRepository<TenantUser> userRepository, 
        ITenantProvider tenantProvider,
        ILogger<RegisterTenantUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(RegisterTenantUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        // Check for existing TenantUser to prevent uniqueness conflict
        if (await _userRepository.Any(u => u.IdentityId == request.IdentityId && u.TenantId == tenantId.Value, cancellationToken))
        {
            return Error.Conflict("User.AlreadyRegistered", "This identity is already registered in this tenant.");
        }

        var result = TenantUser.Create(tenantId.Value, request.IdentityId, request.Name);
        if (result.IsError) return result.Errors;

        _userRepository.Add(result.Value);
        _logger.LogInformation("Identity {IdentityId} registered in Tenant {TenantId} as User {UserId}.", 
            request.IdentityId, tenantId.Value, result.Value.Id);

        return result.Value.Id;
    }
}
