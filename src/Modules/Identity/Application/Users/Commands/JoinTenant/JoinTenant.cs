using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Users.Commands.JoinTenant;

/// <summary>
/// Handles a student joining a specific academy for the first time.
/// Creates a TenantUser and assigns the default 'Student' role.
/// </summary>
public record JoinTenantCommand(
    Guid TenantId,
    string IdentityId,
    string Name) : ICommand<Guid>;

public class JoinTenantCommandValidator : AbstractValidator<JoinTenantCommand>
{
    public JoinTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.IdentityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}

public sealed class JoinTenantCommandHandler(
    IRepository<TenantUser> userRepository,
    ILogger<JoinTenantCommandHandler> logger) : IRequestHandler<JoinTenantCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(JoinTenantCommand request, CancellationToken cancellationToken)
    {
        // 1. Check if already registered
        var user = await userRepository.GetFirst(u => u.IdentityId == request.IdentityId && u.TenantId == request.TenantId, cancellationToken);
        if (user is null)
        {
            // 2. Create TenantUser
            var createResult = TenantUser.Create(request.TenantId, request.IdentityId, request.Name); 
            if (createResult.IsError) return createResult.Errors;
            
            user = createResult.Value;
            userRepository.Add(user);
            
            logger.LogInformation("New user {UserId} created for Identity {IdentityId} in Tenant {TenantId}.", 
                user.Id, request.IdentityId, request.TenantId);
        }
        return user.Id;
    }
}
