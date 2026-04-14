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

    public PolicyEvaluatorService(IEnumerable<IAuthorizationStrategy> strategies)
    {
        _strategies = strategies;
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
    private readonly IRepository<TenantUser> _userRepository;

    public TenantUserAuthorizationStrategy(
        ITenantUserPrincpialAssignmentRepository assignmentRepository,
        IRepository<TenantUser> userRepository)
    {
        _assignmentRepository = assignmentRepository;
        _userRepository = userRepository;
    }

    public AuthorizationMethod Method => AuthorizationMethod.TenantUser;

    public async Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        // 1. Session Integrity Check
        var user = await _userRepository.GetById(context.Id);
        if (user == null) return Error.Forbidden("User.NotFound");
        
        if (context.SessionId.HasValue && user.ActiveSessionId != context.SessionId.Value)
            return Error.Unauthorized("Session.Expired", "Access denied. This session has been invalidated by a newer login.");

        // 2. Construct the Target ARN
        var targetArnResult = ResourceArn.Create(context.ResourceType.ToString(), context.TenantId.ToString(), context.ResourcePath);
        if (targetArnResult.IsError) return Error.Forbidden("Resource.Invalid");
        var targetArn = targetArnResult.Value;

        // 3. Evaluate Scoped Assignments
        // We look for an assignment that matches the target resource
        var assignment = await _assignmentRepository.Get(context.Id, targetArn.ToString());
        if (assignment == null) return Error.Forbidden("Access.Denied", "No matching assignment found for this resource.");

        // We treat the assignment resource as a wildcard pattern (e.g., Course/101/*)
        var scopePattern = ResourcePattern.Create(assignment.Resource.ToString() + "/*").Value;

        foreach (var managedPolicy in assignment.Principal.ManagedPolicies)
        {
            foreach (var statement in managedPolicy.Statements)
            {
                if (statement.Actions.Any(a => AuthorizationHelper.IsActionMatched(context.RequiredPermission, a)) &&
                    scopePattern.IsMatch(targetArn))
                {
                    if (!statement.Effect) return Error.Forbidden("Access.Denied", "Explicit deny in role.");
                    return Result.Success;
                }
            }
        }

        return Error.Forbidden("Access.Denied", "Implicit deny.");
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

        // 1. Evaluate Inline Policies (Direct JSONB overrides)
        foreach (var policy in principal.InlinePolicies)
        {
            foreach (var statement in policy.Statements)
            {
                if (statement.Actions.Any(a => AuthorizationHelper.IsActionMatched(context.RequiredPermission, a)) &&
                    statement.Resources.Any(r => r.IsMatch(targetArn)))
                {
                    if (!statement.Effect) return Error.Forbidden("Access.Denied", "Explicit deny in inline policy.");
                    return Result.Success;
                }
            }
        }

        // 2. Evaluate Managed Policies (Attached directly to this principal instance)
        // We use the Principal's own scope pattern to build the effective policies
        var scope = principal.PrincipalScope?.Value ?? "az:*";

        foreach (var managedPolicy in principal.ManagedPolicies)
        {
            var effectivePolicy = managedPolicy.Build(scope, context.TenantId);
            foreach (var statement in effectivePolicy.Statements)
            {
                if (statement.Actions.Any(a => AuthorizationHelper.IsActionMatched(context.RequiredPermission, a)) &&
                    statement.Resources.Any(r => r.IsMatch(targetArn)))
                {
                    if (!statement.Effect) return Error.Forbidden("Access.Denied", "Explicit deny in managed policy.");
                    return Result.Success;
                }
            }
        }

        return Error.Forbidden("Access.Denied", "Implicit deny.");
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
}
