using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;

/// <summary>
/// Exchanges a global IdentityId (from Cognito) for a Tenant-Scoped JWT.
/// Implements Just-In-Time (JIT) provisioning for new users and registers/verifies devices.
/// </summary>
public record LoginAsTenantUserCommand(
    string IdentityId,
    Guid TenantId,
    string UserName,
    string PublicKey,
    string DeviceName,
    DevicePlatform Platform) : ICommand<TokenResponse>;

public record TokenResponse(string Token, Guid TenantUserId, Guid? DeviceId = null);

public sealed class LoginAsTenantUserCommandHandler : IRequestHandler<LoginAsTenantUserCommand, ErrorOr<TokenResponse>>
{
    private readonly IRepository<TenantUser> _userRepository;
    private readonly ILogger<LoginAsTenantUserCommandHandler> _logger;
    // Note: IJwtProvider will be implemented in Infrastructure
    private readonly IJwtProvider _jwtProvider; 
    private readonly IClock _clock;

    public LoginAsTenantUserCommandHandler(
        IRepository<TenantUser> userRepository, 
        ILogger<LoginAsTenantUserCommandHandler> logger,
        IJwtProvider jwtProvider,
        IClock clock)
    {
        _userRepository = userRepository;
        _logger = logger;
        _jwtProvider = jwtProvider;
        _clock = clock;
    }

    public async Task<ErrorOr<TokenResponse>> Handle(LoginAsTenantUserCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.Now;
        bool isNewUser = false;
        
        // 1. Find or Auto-Create TenantUser
        var user = await _userRepository.GetFirst(u => u.IdentityId == request.IdentityId && u.TenantId == request.TenantId, cancellationToken);

        if (user is null)
        {
             var createResult = TenantUser.Create(request.TenantId, request.IdentityId, request.UserName);
             if (createResult.IsError) return createResult.Errors;
             user = createResult.Value;
             _userRepository.Add(user);
             isNewUser = true;
        }

        // 2. Handle Device Registration & Assignment
        var device = user.Devices.FirstOrDefault(d => d.PublicKey == request.PublicKey);
        if (device is null)
        {
            var registerResult = user.RegisterDevice(request.DeviceName, request.Platform, request.PublicKey, now);
            if (registerResult.IsError) return registerResult.Errors;
            
            device = user.Devices.First(d => d.PublicKey == request.PublicKey);
            
            // If it's their very first device, set it as MainDevice automatically
            if (user.Devices.Count == 1)
            {
                var setMainResult = user.SetMainDevice(device.Id, now, skipCooldown: true);
                if (setMainResult.IsError) return setMainResult.Errors;
            }
        }

        if (!isNewUser)
        {
            _userRepository.Update(user);
        }

        // 3. Generate Scoped JWT
        var token = _jwtProvider.GenerateToken(
            user.Id, 
            user.TenantId, 
            AuthenticationMethod.TenantUser);

        _logger.LogInformation("Identity {IdentityId} logged into Tenant {TenantId} as User {UserId} with Device {DeviceId}.", 
            request.IdentityId, request.TenantId, user.Id, device.Id);

        return new TokenResponse(token, user.Id, device.Id);
    }
}

public interface IJwtProvider
{
    string GenerateToken(Guid id, Guid tenantId, AuthenticationMethod method);
}
