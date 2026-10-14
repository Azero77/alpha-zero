using AlphaZero.Modules.Identity.Application.Auth.Extensions;
using AlphaZero.Modules.Identity.Domain.Repositories;
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
        ICurrentTenantUserRepository currentTenantUserRepository,
        ITenantUserPrincipalAssignmentRepository tenantUserPrincipalAssignmentRepository,
        IDeviceProvider deviceProvider,
        IHttpContextAccessor accessor,
        ITenantProvider tenantProvider
    ) : IAuthorizationContextProvider
    { 
    
        public AuthorizationContext? CurrentAuthorizationContext { get; private set; } = null;
        public ErrorOr<AuthorizationContext?> Get(CancellationToken token = default)
        {
            var httpContext = accessor?.HttpContext;
            var user = httpContext?.User;
            if (user == null || user.Identity?.IsAuthenticated != true)
            {
                CurrentAuthorizationContext = null;
                return (AuthorizationContext?)null;
            }
            // now we are sure there is a user, we will make the authorization context
            AuthorizationContext? result = AuthorizationContext.Empty;
            var idStr = user.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            var authSchemeStr = user.Claims.FirstOrDefault(c => c.Type == "auth_method")?.Value ?? "Principal";
            var tenantId = tenantProvider.GetTenant();

            if (string.IsNullOrEmpty(idStr) || !Enum.TryParse<AuthenticationMethod>(authSchemeStr, true, out var authMethod))
            {
                return Error.Forbidden("Auth.InvalidClaims", "User identity or authentication method is invalid.");
            }

            result = result with
            {
                Id = Guid.Parse(idStr),
                AuthenticationMethod = authSchemeStr,
                TenantId = tenantId,
                DeviceId = deviceProvider.GetDeviceId(),
                IpAddress = accessor?.HttpContext?.Connection.RemoteIpAddress?.ToString()
            };
        
        
            var endpointMetadata = GetEndpointMetadata();
            if (endpointMetadata is not null)
            {
                result = result with
                {
                    RequiredPermission = endpointMetadata.Value.RequiredPermission,
                    ResourcePath = endpointMetadata.Value.arn.ResourcePath,
                    ResourceType = endpointMetadata.Value.arn.Service,
                };
            }

            AuthenticationMethod authMethodEnum= Enum.Parse<AuthenticationMethod>(authSchemeStr);
            if (AuthenticationMethod.Principal == authMethodEnum)
            {
                return CreatePrincipalAuthContext(result);
            }
            if (AuthenticationMethod.TenantUser == authMethodEnum)
            {
                return CreateTenantUserAuthContext(result);
            }
        
            return result;
        }

        private PrincipalUserAuthorizationContext CreatePrincipalAuthContext(AuthorizationContext initial)
        {
            var user = accessor!.HttpContext!.User;
            var principaluser = user.CreatePrincipalFromClaims();
            PrincipalUserAuthorizationContext? result = new()
            {
            
            };

            return result;
        }

        private TenantUserAuthorizationContext CreateTenantUserAuthContext(AuthorizationContext initial)
        {
            var user = accessor!.HttpContext!.User;
            var principaluser = user.CreateTenantUserFromClaims();
            TenantUserAuthorizationContext? result = new()
            {
            
            };
            return result;
        }

        private (string RequiredPermission, ResourceArn arn)? GetEndpointMetadata()
        {
            ResourceArn arn;
            var httpContext = accessor?.HttpContext;
            if (httpContext is null) return null;
            var globalRequirement = httpContext.GetEndpoint()?.Metadata.GetMetadata<GlobalAccessControlRequirement>();
            var tenantScopedRequirement = httpContext.GetEndpoint()?.Metadata.GetMetadata<AccessControlWithTenantRequirement>();
        
            if (globalRequirement is null && tenantScopedRequirement is null) return null;
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