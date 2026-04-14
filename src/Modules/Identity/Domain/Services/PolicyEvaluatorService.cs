using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Services;

public class PolicyEvaluatorService : IPolicyEvaluatorService
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IPrincipalRepository _principalRepository;

    public PolicyEvaluatorService(IPolicyRepository policyRepository, IPrincipalRepository principalRepository)
    {
        _policyRepository = policyRepository;
        _principalRepository = principalRepository;
    }

    public async Task<ErrorOr<Success>> Authorize(Guid prinicapId,
        Guid tenantId,
        string resourcePath,
        ResourceType resourceType,
        string requiredPermission)
    {
        var principal = await _principalRepository.GetById(prinicapId);
        if (principal is null) return Error.Forbidden("Principal.NotFound", "Principal not found.");

        // The Target: A specific ARN
        var targetArnErrorOR = ResourceArn.Create(resourceType.ToString(), tenantId.ToString(), resourcePath);
        if (targetArnErrorOR.IsError)
            return Error.Forbidden("Identity.Application","Invalid Scope");
        ResourceArn targetArn = targetArnErrorOR.Value;
        
        var managedPolicies = await _policyRepository.GetManagedPoliciesForPrincipal(principal.Id);

        bool isAllowed = false;

        // 1. Evaluate Inline Policies
        foreach (var policy in principal.InlinePolicies)
        {
            foreach (var statement in policy.Statements)
            {
                // Boundary Match: Check Action AND ask the Pattern to match the Target ARN
                if (statement.Actions.Any(a => IsActionMatched(requiredPermission, a)) &&
                    statement.Resources.Any(pattern => pattern.IsMatch(targetArn)))
                {
                    if (!statement.Effect) return Error.Forbidden("Access.Denied", "Explicit deny.");
                    isAllowed = true;
                }
            }
        }

        // 2. Evaluate Managed Policies via Principal Template Scope
        if (managedPolicies is not null && principal.PrincipalScope != null)
        {
            foreach (var policy in managedPolicies)
            {
                foreach (var statement in policy.Statements)
                {
                    // Check if the target is within the user's principal scope pattern
                    if (statement.Actions.Any(a => IsActionMatched(requiredPermission, a)) &&
                        principal.PrincipalScope.IsMatch(targetArn))
                    {
                        if (!statement.Effect) return Error.Forbidden("Access.Denied", "Explicit deny.");
                        isAllowed = true;
                    }
                }
            }
        }

        return isAllowed ? Result.Success : Error.Forbidden("Access.Denied", "Implicit deny.");
    }

    private bool IsActionMatched(string requiredPermission, string givenAction)
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
