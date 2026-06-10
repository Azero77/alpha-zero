using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlphaZero.Modules.Identity.Domain.Services;

/// <summary>
/// Provides methods for creating authorization contexts used to evaluate user permissions within the application.
/// </summary>
public class AuthorizationContextFactory(ICurrentTenantUserRepository currentTenantUserRepository,
    ITenantUserPrincpialAssignmentRepository tenantUserPrincpialAssignmentRepository,
    IPrincipalRepository principalRepository,
    IDeviceProvider deviceProvider,
    IHttpContextAccessor accessor
    ) : IAuthorizationContextFactory
{
    public AuthorizationContext? CurrentAuthorizationContext { get; private set; } = null;
    public async Task<ErrorOr<AuthorizationContext>> Create(ResourceArn arn, AuthenticationMethod authenticationMethod, string id)
    {

        var context = new AuthorizationContext()
        {
            AuthenticationMethod = authenticationMethod.ToString(),
            Id = Guid.Parse(id),
            ResourcePath = arn.ResourcePath,
            ResourceType = arn.Service,
            TenantId = Guid.Parse(arn.TenantIdString),
            DeviceId = deviceProvider.GetDeviceId(),
            IpAddress = accessor?.HttpContext?.Connection.RemoteIpAddress?.ToString()
        };
        if (AuthenticationMethod.Principal == authenticationMethod)
        {
            var principalResult = await principalRepository.GetById(Guid.Parse(id));
            if (principalResult is null)
                return Error.NotFound();
            return Create(arn, principalResult, context);
        }
        else if (AuthenticationMethod.TenantUser == authenticationMethod)
        {
            var tenantUser = await currentTenantUserRepository.GetCurrentUser();
            if (tenantUser is null || tenantUser.UserId.ToString() != id)
            {
                return Error.NotFound();
            }

            var assignment = await tenantUserPrincpialAssignmentRepository.Get(tenantUser.UserId, arn.Value);
            if (assignment is null)
                return Error.Forbidden();
            var result =  Create(arn, assignment, context);
            if (!result.IsError)
                CurrentAuthorizationContext = result.Value;
            return result;
        }
        return Error.Forbidden();
    }


    public ErrorOr<AuthorizationContext> Create(ResourceArn arn, Principal principal, AuthorizationContext contextInitial)
    {
        return contextInitial;
    }
    public ErrorOr<AuthorizationContext> Create(ResourceArn arn, TenantUserPrinciaplAssignment tenantUserPrinciaplAssignment, AuthorizationContext contextInitial)
    {
        AuthorizationContext result = contextInitial with
        {
            UserMainDeviceId = tenantUserPrinciaplAssignment.TenantUser.MainDeviceId?.ToString(),
        };

        return result;
    }
}


