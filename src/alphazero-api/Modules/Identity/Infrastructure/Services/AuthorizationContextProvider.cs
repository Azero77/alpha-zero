using AlphaZero.Modules.Identity.Application.Auth.Extensions;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace AlphaZero.Modules.Identity.Infrastructure.Services
{
    /// <summary>
    /// Provides methods for creating authorization contexts used to evaluate user permissions within the application.
    /// </summary>
    public class AuthorizationContextProvider(
        IDeviceProvider deviceProvider,
        IHttpContextAccessor accessor,
        ITenantProvider tenantProvider
    ) : IAuthorizationContextProvider
    { 
        public AuthorizationContext? CurrentAuthorizationContext { get; private set; }

        public ErrorOr<AuthorizationContext?> Get(CancellationToken token = default)
        {
            if (CurrentAuthorizationContext is not null)
            {
                return CurrentAuthorizationContext;
            }

            var httpContext = accessor?.HttpContext;
            var user = httpContext?.User;
            var endpointMetadata = GetEndpointMetadata();

            if (user == null || user.Identity?.IsAuthenticated != true)
            {
                if (endpointMetadata is not null)
                {
                    return Error.Forbidden("Auth.Unauthenticated", "User identity or authentication is required for this resource.");
                }

                CurrentAuthorizationContext = null;
                return (AuthorizationContext?)null;
            }

            var idStr = user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.Sub || c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            var authSchemeStr = user.Claims.FirstOrDefault(c => c.Type == "auth_method")?.Value ?? "Principal";
            var tenantId = tenantProvider.GetTenant();

            if (string.IsNullOrEmpty(idStr) || !Enum.TryParse<AuthenticationMethod>(authSchemeStr, true, out var authMethod))
            {
                return Error.Forbidden("Auth.InvalidClaims", "User identity or authentication method is invalid.");
            }

            var userId = Guid.Parse(idStr);

            AuthorizationContext result = authMethod switch
            {
                AuthenticationMethod.TenantUser => CreateTenantUserAuthContext(user, userId, tenantId),
                AuthenticationMethod.Principal => CreatePrincipalAuthContext(user, userId, tenantId),
                _ => AuthorizationContext.Empty with
                {
                    Id = userId,
                    AuthenticationMethod = authSchemeStr,
                    TenantId = tenantId,
                    DeviceId = deviceProvider.GetDeviceId(),
                    IpAddress = accessor?.HttpContext?.Connection.RemoteIpAddress?.ToString()
                }
            };

            if (endpointMetadata is not null)
            {
                result = result with
                {
                    RequiredPermission = endpointMetadata.Value.RequiredPermission,
                    ResourcePath = endpointMetadata.Value.arn.ResourcePath,
                    ResourceType = endpointMetadata.Value.arn.Service,
                };
            }

            CurrentAuthorizationContext = result;
            return result;
        }

        private PrincipalUserAuthorizationContext CreatePrincipalAuthContext(ClaimsPrincipal user, Guid id, Guid? tenantId)
        {
            var principalDto = user.ToPrincipalDTO();

            return new PrincipalUserAuthorizationContext
            {
                Id = id,
                PrincipalId = id,
                Username = principalDto?.Username ?? user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.Username)?.Value ?? string.Empty,
                Name = principalDto?.Name ?? user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.Name)?.Value ?? string.Empty,
                PrincipalType = principalDto?.PrincipalType ?? user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.PrincipalType)?.Value ?? "User",
                IsManaged = principalDto?.IsManaged ?? false,
                IsGlobal = principalDto?.IsGlobal ?? false,
                PrincipalScope = principalDto?.PrincipalScope ?? user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.PrincipalScope)?.Value,
                AuthenticationMethod = AuthenticationMethod.Principal.ToString(),
                TenantId = tenantId ?? principalDto?.TenantId,
                DeviceId = deviceProvider.GetDeviceId(),
                IpAddress = accessor?.HttpContext?.Connection.RemoteIpAddress?.ToString()
            };
        }

        private TenantUserAuthorizationContext CreateTenantUserAuthContext(ClaimsPrincipal user, Guid id, Guid? tenantId)
        {
            var tenantUserDto = user.ToTenantUserDTO();
            var mainDeviceId = tenantUserDto?.MainDeviceId?.ToString()
                ?? user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.MainDeviceId)?.Value;

            return new TenantUserAuthorizationContext
            {
                Id = id,
                IdentityId = tenantUserDto?.IdentityId ?? user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.IdentityId)?.Value ?? id.ToString(),
                Name = tenantUserDto?.Name ?? user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.Name)?.Value ?? string.Empty,
                AuthenticationMethod = AuthenticationMethod.TenantUser.ToString(),
                TenantId = tenantId ?? tenantUserDto?.TenantId,
                DeviceId = deviceProvider.GetDeviceId(),
                UserMainDeviceId = mainDeviceId,
                MainDeviceId = mainDeviceId,
                DeviceName = tenantUserDto?.DeviceName,
                DevicePlatform = tenantUserDto?.DevicePlatform,
                PublicKey = tenantUserDto?.PublicKey,
                IpAddress = accessor?.HttpContext?.Connection.RemoteIpAddress?.ToString()
            };
        }

        private (string RequiredPermission, ResourceArn arn)? GetEndpointMetadata()
        {
            var httpContext = accessor?.HttpContext;
            if (httpContext is null) return null;
            var globalRequirement = httpContext.GetEndpoint()?.Metadata.GetMetadata<GlobalAccessControlRequirement>();
            var tenantScopedRequirement = httpContext.GetEndpoint()?.Metadata.GetMetadata<AccessControlWithTenantRequirement>();
        
            if (globalRequirement is null && tenantScopedRequirement is null) return null;

            ResourceArn arn;
            if (globalRequirement != null)
            {
                arn = globalRequirement.resourceArnFactory(httpContext.Request!);
            }
            else //tenant Requirement
            {
                var currentTenant = tenantProvider.GetTenant();
                if (currentTenant == null)
                    return null;
                arn = tenantScopedRequirement!.resourceArnFactory(httpContext.Request!, currentTenant.Value);
            }
            string? permission = globalRequirement?.Action ?? tenantScopedRequirement?.Action;

            if (permission is null) return null;
            return (permission, arn);
        }
    }
}