using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Services;

public class PolicyEvaluatorService : IPolicyEvaluatorService
{
    private readonly IEnumerable<IAuthorizationStrategy> _strategies;
    private readonly IRepository<TenantUser> _userRepository;

    public PolicyEvaluatorService(
        IEnumerable<IAuthorizationStrategy> strategies,
        IRepository<TenantUser> userRepository)
    {
        _strategies = strategies;
        _userRepository = userRepository;
    }

    public async Task<ErrorOr<Success>> Authorize(
        Guid id, 
        Guid tenantId, 
        string resourcePath, 
        ResourceType resourceType, 
        string requiredPermission,
        string authMethod,
        Guid? sessionId = null)
    {
        // 1. Centralized Session Check for TenantUsers
        if (authMethod.Equals(AuthorizationMethod.TenantUser.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var user = await _userRepository.GetById(id);
            if (user == null) return Error.Forbidden("User.NotFound");
            
            if (sessionId.HasValue && user.ActiveSessionId != sessionId.Value)
                return Error.Unauthorized("Session.Expired", "Access denied. This session has been invalidated by a newer login.");
        }

        var strategy = _strategies.FirstOrDefault(s => s.Method.ToString().Equals(authMethod, StringComparison.OrdinalIgnoreCase));
        
        if (strategy == null)
            return Error.Forbidden("Identity.Auth", $"No strategy found for auth method: {authMethod}");

        var context = new AuthorizationContext
        {
            Id = id,
            TenantId = tenantId,
            ResourcePath = resourcePath,
            ResourceType = resourceType,
            RequiredPermission = requiredPermission,
            SessionId = sessionId
        };

        return await strategy.Authorize(context);
    }
}

public interface IAuthorizationStrategy
{
    AuthorizationMethod Method { get; }
    Task<ErrorOr<Success>> Authorize(AuthorizationContext context);
}

public class AuthorizationContext
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string ResourcePath { get; init; } = string.Empty;
    public ResourceType ResourceType { get; init; }
    public string RequiredPermission { get; init; } = string.Empty;
    public Guid? SessionId { get; init; }
}

public class TenantUserAuthorizationStrategy : IAuthorizationStrategy
{
    private readonly ITenantUserPrincpialAssignmentRepository _assignmentRepository;

    public TenantUserAuthorizationStrategy(ITenantUserPrincpialAssignmentRepository assignmentRepository)
    {
        _assignmentRepository = assignmentRepository;
    }

    public AuthorizationMethod Method => AuthorizationMethod.TenantUser;

    public async Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        // 1. Construct the Target ARN
        var targetArnResult = ResourceArn.Create(context.ResourceType.ToString(), context.TenantId.ToString(), context.ResourcePath);
        if (targetArnResult.IsError) return Error.Forbidden("Resource.Invalid");
        var targetArn = targetArnResult.Value;

        // 2. Evaluate Scoped Assignments
        var assignment = await _assignmentRepository.Get(context.Id, targetArn.ToString());
        if (assignment == null) return Error.Forbidden("Access.Denied", "No matching assignment found for this resource.");

        var scopePattern = ResourcePattern.Create(assignment.Resource.ToString() + "/*").Value;

        bool isAllowed = false;
        foreach (var managedPolicy in assignment.Principal.ManagedPolicies)
        {
            foreach (var statement in managedPolicy.Statements)
            {
                if (statement.Actions.Any(a => AuthorizationHelper.IsActionMatched(context.RequiredPermission, a)) &&
                    scopePattern.IsMatch(targetArn))
                {
                    if (!statement.Effect) return Error.Forbidden("Access.Denied", "Explicit deny in role.");
                    isAllowed = true;
                }
            }
        }

        return isAllowed ? Result.Success : Error.Forbidden("Access.Denied", "Implicit deny.");
    }
}

public class PrincipalUserAuthorizationStrategy : IAuthorizationStrategy
{
    private readonly IPrincipalRepository _principalRepository;

    public PrincipalUserAuthorizationStrategy(IPrincipalRepository principalRepository)
    {
        _principalRepository = principalRepository;
    }

    public AuthorizationMethod Method => AuthorizationMethod.Principal;

    public async Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        var principal = await _principalRepository.GetById(context.Id);
        if (principal is null) return Error.Forbidden("Principal.NotFound");

        var targetArnResult = ResourceArn.Create(context.ResourceType.ToString(), context.TenantId.ToString(), context.ResourcePath);
        if (targetArnResult.IsError) return Error.Forbidden("Resource.Invalid");
        var targetArn = targetArnResult.Value;

        bool isAllowed = false;

        // 1. Evaluate Inline Policies
        foreach (var policy in principal.InlinePolicies)
        {
            foreach (var statement in policy.Statements)
            {
                if (AuthorizationHelper.IsStatementMatch(statement, context.RequiredPermission, targetArn))
                {
                    if (!statement.Effect) return Error.Forbidden("Access.Denied", "Explicit deny in inline policy.");
                    isAllowed = true;
                }
            }
        }

        // 2. Evaluate Managed Policies
        var scope = principal.PrincipalScope?.Value ?? "az:*";
        foreach (var managedPolicy in principal.ManagedPolicies)
        {
            var effectivePolicy = managedPolicy.Build(scope, context.TenantId);
            foreach (var statement in effectivePolicy.Statements)
            {
                if (AuthorizationHelper.IsStatementMatch(statement, context.RequiredPermission, targetArn))
                {
                    if (!statement.Effect) return Error.Forbidden("Access.Denied", "Explicit deny in managed policy.");
                    isAllowed = true;
                }
            }
        }

        return isAllowed ? Result.Success : Error.Forbidden("Access.Denied", "Implicit deny.");
    }
}

public static class AuthorizationHelper
{
    public static bool IsActionMatched(string requiredPermission, string givenAction)
    {
        if (requiredPermission.Equals(givenAction, StringComparison.OrdinalIgnoreCase)) return true;
        if (givenAction.EndsWith("*"))
        {
            var prefix = givenAction.Substring(0, givenAction.Length - 1);
            return requiredPermission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public static bool IsStatementMatch(PolicyStatement statement, string requiredPermission, ResourceArn targetArn)
    {
        return statement.Actions.Any(a => IsActionMatched(requiredPermission, a)) &&
               statement.Resources.Any(r => r.IsMatch(targetArn));
    }
}
