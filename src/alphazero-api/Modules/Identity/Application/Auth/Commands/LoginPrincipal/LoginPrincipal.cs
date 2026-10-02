using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Application.Auth.Extensions;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
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
    private readonly IJwtProvider _jwtProvider;
    private readonly ILogger<LoginPrincipalCommandHandler> _logger;
    private readonly PrincipalLoginService _principalLoginService;
    private readonly IClock _clock;

    public LoginPrincipalCommandHandler(
        IPrincipalRepository principalRepository,
        IJwtProvider jwtProvider,
        PrincipalLoginService principalLoginService,
        IClock clock,
        ILogger<LoginPrincipalCommandHandler> logger)
    {
        _principalRepository = principalRepository;
        _jwtProvider = jwtProvider;
        _logger = logger;
        _principalLoginService = principalLoginService;
        _clock = clock;
    }

    public async Task<ErrorOr<TokenResponse>> Handle(LoginPrincipalCommand request, CancellationToken cancellationToken)
    {
        var principal = await _principalRepository.GetFirst(p => p.Username == request.Username && p.TenantId == request.TenantId, cancellationToken);
        if (principal is null)
            return Error.Unauthorized("Auth.NotFoundCredentials","Principal not found.");
        var loginRequest = _principalLoginService.Login(principal, request.Password);
        if (loginRequest.IsError)
            return loginRequest.Errors;

        var additionalClaims = principal.ToClaims(_clock);

        var token = _jwtProvider.GenerateToken(
            principal.Id,
            principal.TenantId,
            AuthenticationMethod.Principal,
            additionalClaims);

        _logger.LogInformation("Principal {Username} logged into Tenant {TenantId}.", request.Username, request.TenantId);

        return new TokenResponse(token, principal.Id);
    }
}

