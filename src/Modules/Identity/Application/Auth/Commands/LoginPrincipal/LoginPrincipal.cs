using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Auth.Commands.LoginPrincipal;

public record LoginPrincipalCommand(
    Guid TenantId,
    string Username,
    string Password) : ICommand<TokenResponse>;

public sealed class LoginPrincipalCommandHandler : IRequestHandler<LoginPrincipalCommand, ErrorOr<TokenResponse>>
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly ILogger<LoginPrincipalCommandHandler> _logger;

    public LoginPrincipalCommandHandler(
        IPrincipalRepository principalRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        ILogger<LoginPrincipalCommandHandler> logger)
    {
        _principalRepository = principalRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<TokenResponse>> Handle(LoginPrincipalCommand request, CancellationToken cancellationToken)
    {
        var principal = await _principalRepository.GetFirst(p => p.Username == request.Username && p.TenantId == request.TenantId, cancellationToken);

        if (principal is null || !_passwordHasher.VerifyPassword(request.Password, principal.PasswordHash))
        {
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid username or password for this tenant.");
        }

        // Principals in our system don't use ActiveSessionId for enforcement currently, 
        // but we can generate a session ID for the JWT.
        var sessionId = Guid.NewGuid();

        var token = _jwtProvider.GenerateToken(
            principal.Id,
            principal.TenantId,
            sessionId,
            AuthorizationMethod.Principal);

        _logger.LogInformation("Principal {Username} logged into Tenant {TenantId}.", request.Username, request.TenantId);

        return new TokenResponse(token, principal.Id, sessionId);
    }
}
