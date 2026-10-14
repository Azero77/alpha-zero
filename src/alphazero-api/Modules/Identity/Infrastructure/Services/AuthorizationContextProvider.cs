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
            var endpoint = httpContext?.GetEndpoint();
            var globalRequirement = endpoint?.Metadata.GetMetadata<GlobalAccessControlRequirement>();
            var tenantScopedRequirement = endpoint?.Metadata.GetMetadata<AccessControlWithTenantRequirement>();
            bool hasAccessControlRequirement = globalRequirement is not null || tenantScopedRequirement is not null;

            if (user == null || user.Identity?.IsAuthenticated != true)
            {
                if (hasAccessControlRequirement)
                {
                    return Error.Forbidden("Auth.Unauthenticated", "User identity or authentication is required for this resource.");
                }

                CurrentAuthorizationContext = null;
                return (AuthorizationContext?)null;
            }

            var idStr = user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.Sub || c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            var authSchemeStr = user.Claims.FirstOrDefault(c => c.Type == "auth_method")?.Value ?? "Principal";
            var tenantId = tenantProvider.GetTenant();

            if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var userId) || !Enum.TryParse<AuthenticationMethod>(authSchemeStr, true, out var authMethod))
            {
                return Error.Forbidden("Auth.InvalidClaims", "User identity or authentication method is invalid.");
            }
 
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

            var endpointMetadataResult = GetEndpointMetadata(httpContext!, globalRequirement, tenantScopedRequirement, tenantId);
            if (endpointMetadataResult.IsError)
            {
                return endpointMetadataResult.Errors;
            }

            if (endpointMetadataResult.Value is not null)
            {
                var metadata = endpointMetadataResult.Value.Value;
                result = result with
                {
                    RequiredPermission = metadata.RequiredPermission,
                    ResourcePath = metadata.arn.ResourcePath,
                    ResourceType = metadata.arn.Service,
                };
            }

            CurrentAuthorizationContext = result;
            return result;
        }

        private PrincipalUserAuthorizationContext CreatePrincipalAuthContext(ClaimsPrincipal user, Guid id, Guid? tenantId)
        {
            var principalDto = user.ToPrincipalDTO();
            var mainDeviceId = user.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.MainDeviceId)?.Value;

            return new PrincipalUserAuthorizationContext
            {
                Id = id,
                PrincipalId = id,
                Username = principalDto?.Username ?? string.Empty,
                Name = principalDto?.Name ?? string.Empty,
                PrincipalType = principalDto?.PrincipalType ?? "User",
                IsManaged = principalDto?.IsManaged ?? false,
                IsGlobal = principalDto?.IsGlobal ?? false,
                PrincipalScope = principalDto?.PrincipalScope,
                AuthenticationMethod = nameof(AuthenticationMethod.Principal),
                TenantId = tenantId ?? principalDto?.TenantId,
                DeviceId = deviceProvider.GetDeviceId(),
                UserMainDeviceId = mainDeviceId,
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
                IdentityId = tenantUserDto?.IdentityId!,
                Name = tenantUserDto?.Name ?? string.Empty,
                AuthenticationMethod = nameof(AuthenticationMethod.TenantUser),
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

        private ErrorOr<(string RequiredPermission, ResourceArn arn)?> GetEndpointMetadata(
            HttpContext httpContext,
            GlobalAccessControlRequirement? globalRequirement,
            AccessControlWithTenantRequirement? tenantScopedRequirement,
            Guid? currentTenant)
        {
            if (globalRequirement is null && tenantScopedRequirement is null) 
                return (ValueTuple<string, ResourceArn>?)null;

            var requestObj = (httpContext.Items.TryGetValue("__FastEndpoints_Request__", out var fastReq) && fastReq is not null)
                ? fastReq
                : (object)httpContext.Request;

            ResourceArn arn;
            if (globalRequirement != null)
            {
                arn = globalRequirement.resourceArnFactory(requestObj);
            }
            else // tenant Requirement
            {
                if (currentTenant == null || currentTenant == Guid.Empty)
                    return Error.Forbidden("Auth.TenantMissing", "Tenant identity is required for this resource.");

                arn = tenantScopedRequirement!.resourceArnFactory(requestObj, currentTenant.Value);
            }
            string? permission = globalRequirement?.Action ?? tenantScopedRequirement?.Action;

            if (permission is null) return (ValueTuple<string, ResourceArn>?)null;
            return (permission, arn);
        }
    }
}