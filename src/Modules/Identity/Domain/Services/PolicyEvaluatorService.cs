using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Services;

/// <summary>
/// Provides methods for evaluating authorization policies and determining whether a principal has the required
/// permissions to access a specified resource.
/// </summary>
/// <remarks>This service acts as a central point for policy evaluation, delegating policy data retrieval to the
/// configured policy repository. It is typically used in scenarios where access control decisions must be enforced
/// based on dynamic policies associated with principals and resources.</remarks>
public class PolicyEvaluatorService
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
        
        var resourceArn = new ResourceArn(resourceType.ToString(), tenantId, resourcePath);
        var managedPolicies = await _policyRepository.GetManagedPoliciesForPrincipal(principal.Id);

        // IAM Standard: Default Deny
        bool isAllowed = false;

        // 1. Evaluate Inline Policies (Custom to this user)
        if (principal.InlinePolicies.Any())
        {
            foreach (var policy in principal.InlinePolicies)
            {
                foreach (var statement in policy.Statements)
                {
                    if (statement.Actions.Any(a => IsActionMatched(requiredPermission, a)) && 
                        statement.Resources.Any(r => resourceArn.IsMatchedBy(r)))
                    {
                        // IAM Standard: Explicit Deny ALWAYS overrides and short-circuits
                        if (!statement.Effect) 
                            return Error.Forbidden("Access.Denied", "Explicit deny in inline policy.");
                        
                        isAllowed = true;
                    }
                }
            }
        }

        // 2. Evaluate Managed Policies (Templates scoped by the Principal's URN)
        if (managedPolicies is not null)
        {
            foreach (var policy in managedPolicies)
            {
                foreach (var statement in policy.Statements)
                {
                    // The magic : The Managed Policy doesn't have Resources, 
                    // it applies dynamically to the Principal's Scope!
                    if (statement.Actions.Any(a => IsActionMatched(requiredPermission, a)) && 
                        resourceArn.IsMatchedBy(principal.PrincipalScopeUrn ?? ""))
                    {
                        // Explicit Deny Short-circuit
                        if (!statement.Effect) 
                            return Error.Forbidden("Access.Denied", "Explicit deny in managed policy.");
                        
                        isAllowed = true;
                    }
                }
            }
        }

        // 3. Final Decision
        if (isAllowed)
            return Result.Success;

        // Implicit Deny (No explicit allow was found)
        return Error.Forbidden("Access.Denied", "Implicit deny. No matching allow policy found.");
    }

    private bool IsActionMatched(string requiredPermission, string givenAction)
    {
        // 1. Exact Match (Case Insensitive)
        if (requiredPermission.Equals(givenAction, StringComparison.OrdinalIgnoreCase)) 
            return true;

        // 2. Wildcard Match
        if (givenAction.EndsWith("*"))
        {
            var prefix = givenAction.Substring(0, givenAction.Length - 1);
            return requiredPermission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
    }
}
