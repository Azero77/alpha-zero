using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace AlphaZero.Modules.Identity.Domain.Services;

/// <summary>
/// Provides methods for creating authorization contexts used to evaluate user permissions within the application.
/// </summary>
public class AuthorizationContextFactory(
    ICurrentTenantUserRepository currentTenantUserRepository,
    ITenantUserPrincipalAssignmentRepository tenantUserPrincipalAssignmentRepository,
    IDeviceProvider deviceProvider,
    IHttpContextAccessor accessor
) : IAuthorizationContextFactory
{
    public AuthorizationContext? CurrentAuthorizationContext { get; private set; } = null;

    public async Task<ErrorOr<AuthorizationContext>> Create(
        string requiredPermission,
        ResourceArn arn,
        AuthenticationMethod authenticationMethod,
        string id,
        CancellationToken cancellationToken)
    {
        var context = new AuthorizationContext()
        {
            AuthenticationMethod = authenticationMethod.ToString(),
            Id = Guid.Parse(id),
            ResourcePath = arn.ResourcePath,
            ResourceType = arn.Service,
            TenantId = arn.TenantIdString.Equals(ResourceArn.GlobalTenant, StringComparison.OrdinalIgnoreCase) 
                        ? Guid.Empty 
                        : Guid.Parse(arn.TenantIdString),
            DeviceId = deviceProvider.GetDeviceId(),
            IpAddress = accessor?.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            RequiredPermission = requiredPermission
        };

        if (AuthenticationMethod.Principal == authenticationMethod)
        {
            var result = Create(arn, context);
            if (!result.IsError)
                CurrentAuthorizationContext = result.Value;
            return result;
        }
        else if (AuthenticationMethod.TenantUser == authenticationMethod)
        {
            var tenantUser = await currentTenantUserRepository.GetCurrentUser();
            if (tenantUser is null || tenantUser.UserId.ToString() != id)
            {
                return Error.NotFound();
            }

            var assignment = await tenantUserPrincipalAssignmentRepository.GetActiveAssignment(tenantUser.UserId, arn.Value, cancellationToken);
            if (assignment is null)
                return Error.Forbidden();
            var result = Create(arn, assignment, context);
            if (!result.IsError)
                CurrentAuthorizationContext = result.Value;
            return result;
        }

        return Error.Forbidden();
    }

    public async Task<ErrorOr<AuthorizationContext?>> GetOrCreateCurrentContextAsync(
        string? requiredPermission = null,
        ResourceArn? arn = null,
        CancellationToken token = default)
    {
        var httpContext = accessor?.HttpContext;
        var user = httpContext?.User;
        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            CurrentAuthorizationContext = null;
            return (AuthorizationContext?)null;
        }

        var idStr = user.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        var authSchemeStr = user.Claims.FirstOrDefault(c => c.Type == "auth_method")?.Value ?? "Principal";

        if (string.IsNullOrEmpty(idStr) || !Enum.TryParse<AuthenticationMethod>(authSchemeStr, true, out var authMethod))
        {
            return Error.Forbidden("Auth.InvalidClaims", "User identity or authentication method is invalid.");
        }

        if (arn != null)
        {
            var createResult = await Create(requiredPermission ?? string.Empty, arn, authMethod, idStr, token);
            if (createResult.IsError)
                return createResult.Errors;

            CurrentAuthorizationContext = createResult.Value;
            return createResult.Value;
        }

        var tenantIdStr = user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.TenantId)?.Value;
        Guid? tenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : null;

        var context = new AuthorizationContext()
        {
            AuthenticationMethod = authMethod.ToString(),
            Id = Guid.Parse(idStr),
            ResourcePath = string.Empty,
            ResourceType = string.Empty,
            TenantId = tenantId,
            DeviceId = deviceProvider.GetDeviceId(),
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            RequiredPermission = requiredPermission
        };

        if (authMethod == AuthenticationMethod.TenantUser)
        {
            var tenantUser = await currentTenantUserRepository.GetCurrentUser();
            if (tenantUser is not null)
            {
                context = context with
                {
                    UserMainDeviceId = tenantUser.MainDeviceId?.ToString()
                };
            }
        }

        CurrentAuthorizationContext = context;
        return context;
    }

    public ErrorOr<AuthorizationContext> Create(ResourceArn arn, AuthorizationContext contextInitial)
    {
        return contextInitial;
    }

    public ErrorOr<AuthorizationContext> Create(ResourceArn arn, TenantUserPrincipalAssignment tenantUserPrincipalAssignment, AuthorizationContext contextInitial)
    {
        AuthorizationContext result = contextInitial with
        {
            UserMainDeviceId = tenantUserPrincipalAssignment.TenantUser.MainDeviceId?.ToString(),
        };

        return result;
    }
}
