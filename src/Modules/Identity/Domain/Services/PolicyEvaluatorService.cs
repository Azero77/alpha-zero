using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Domain;
using Amazon.Auth.AccessControlPolicy;
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
        if (principal is null) return Error.NotFound("Principal.NotFound", "Principal not found.");
        var resourceArn = new ResourceArn(resourceType.ToString(), tenantId, resourcePath);

        var managedPolicies = await _policyRepository.GetManagedPoliciesForPrincipal(principal.Id);

        bool isAllowedByInline = false;
        bool isAllowedByManaged = false;
        bool isDeniedByManaged = false;
        bool isDeniedByInline = false;
       

        if (principal.InlinePolicies.Any())
        {
            foreach (var policy in principal.InlinePolicies)
            {
                foreach (var statement in policy.Statements)
                {
                    if (statement.Actions.Any(a => IsActionMatched(requiredPermission, a)) && statement.Resources.Any(r => resourceArn.IsMatchedBy(r)))
                    {
                        if(statement.Effect)
                            isAllowedByInline = true;
                        else
                            isDeniedByInline = true;

                    }
                }
            }
        }
        if (isDeniedByInline)
            return Error.NotFound();

        if (isAllowedByInline)
            return Result.Success;
        if (managedPolicies is not null)
        {
            foreach (var policy in managedPolicies)
            {
                foreach (var statement in policy.Statements)
                {
                    if (statement.Actions.Any(a => IsActionMatched(requiredPermission, a)) && resourceArn.IsMatchedBy(principal.PrincipalScopeUrn ?? ""))
                    {
                        if (statement.Effect)
                            isAllowedByManaged = true;
                        else
                            isDeniedByManaged = true;

                    }
                }
            }
        }

        if (isDeniedByManaged)
            return Error.NotFound();

        if (isAllowedByManaged)
            return Result.Success;
        return Error.Forbidden();

    }

    private bool IsActionMatched(string requiredPermission, string givenAction)
    {
        if(requiredPermission == givenAction) return true;

        if(givenAction.EndsWith("*"))
        {
            var prefix = givenAction.Substring(0, givenAction.Length - 1);
            return requiredPermission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }


}
