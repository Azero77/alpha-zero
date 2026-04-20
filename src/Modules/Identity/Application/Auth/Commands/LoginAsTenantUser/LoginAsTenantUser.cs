using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;

/// <summary>
/// Exchanges a global IdentityId (from Cognito) for a Tenant-Scoped JWT.
/// </summary>
public record LoginAsTenantUserCommand(
    string IdentityId,
    Guid TenantId,
    string UserName) : ICommand<TokenResponse>;

public record TokenResponse(string Token, Guid TenantUserId, Guid SessionId);

public sealed class LoginAsTenantUserCommandHandler : IRequestHandler<LoginAsTenantUserCommand, ErrorOr<TokenResponse>>
{
    private readonly IRepository<TenantUser> _userRepository;
    private readonly ILogger<LoginAsTenantUserCommandHandler> _logger;
    // Note: IJwtProvider will be implemented in Infrastructure
    private readonly IJwtProvider _jwtProvider; 

    public LoginAsTenantUserCommandHandler(
        IRepository<TenantUser> userRepository, 
        ILogger<LoginAsTenantUserCommandHandler> logger,
        IJwtProvider jwtProvider)
    {
        _userRepository = userRepository;
        _logger = logger;
        _jwtProvider = jwtProvider;
    }

    public async Task<ErrorOr<TokenResponse>> Handle(LoginAsTenantUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Find or Auto-Create TenantUser
        // In your SaaS, enrollment triggers this, but we'll ensure it exists here for robustness
        var user = await _userRepository.GetFirst(u => u.IdentityId == request.IdentityId && u.TenantId == request.TenantId, cancellationToken);

        if (user is null)
        {
             // If not enrolled/registered in this tenant, they can't log in
             return Error.Forbidden("Auth.NotEnrolled", "User is not a member of this tenant.");
        }

        // 2. Refresh Session (Invalidates other devices)
        var sessionId = user.RefreshSession();
        _userRepository.Update(user);

        // 3. Generate Scoped JWT
        var token = _jwtProvider.GenerateToken(
            user.Id, 
            user.TenantId, 
            sessionId, 
            AuthorizationMethod.TenantUser);

        _logger.LogInformation("Identity {IdentityId} logged into Tenant {TenantId} as User {UserId}.", 
            request.IdentityId, request.TenantId, user.Id);

        return new TokenResponse(token, user.Id, sessionId);
    }
}

public interface IJwtProvider
{
    string GenerateToken(Guid id, Guid tenantId, Guid sessionId, AuthorizationMethod method);
}
