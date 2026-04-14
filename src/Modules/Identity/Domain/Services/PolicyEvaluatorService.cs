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

    protected bool IsActionMatched(string requiredPermission, string givenAction)
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


public interface IAuthorizationStrategy
{
    Task<ErrorOr<Success>> Authorize(AuthorizationContext context);
}
public static class AuthorizationHelper
{

    public static bool IsActionMatched(string requiredPermission, string givenAction)
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

public class AuthorizationContext
{
    public Guid Id { get; set;  } //either principal specific Id, tenant User id
    public Guid TenantId { get; set;  }
    public string ResourcePath { get; set; } = string.Empty;
    public string RequiredPermission { get; set; } = string.Empty;
    public ResourceType ResourceType { get; set; }
}

public class TenatUserAuthorizationStrategy : IAuthorizationStrategy
{
    private readonly ITenantUserPrincpialAssignmentRepository _tenantUserPrincpialAssignmentRepository;

    public TenatUserAuthorizationStrategy(ITenantUserPrincpialAssignmentRepository tenantUserPrincpialAssignmentRepository)
    {
        _tenantUserPrincpialAssignmentRepository = tenantUserPrincpialAssignmentRepository;
    }

    public Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        var scope = context.ResourcePath.TrimEnd('*', '/'); // for getting the resource without any pattern inside
        var resourceArn = new ResourceArn(context.ResourceType.ToString() , context.TenantId,scope);
        _tenantUserPrincpialAssignmentRepository.Get(context.Id, resourceArn.ToString());
        throw new NotImplementedException();
    }
}

public class PrincipalUserAuthorizationStrategy : IAuthorizationStrategy
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IPrincipalRepository _principalRepository;

    public PrincipalUserAuthorizationStrategy(IPolicyRepository policyRepository, IPrincipalRepository principalRepository)
    {
        _policyRepository = policyRepository;
        _principalRepository = principalRepository;
    }

    public async Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        var principal = await _principalRepository.GetById(context.Id);
        if (principal is null) return Error.Forbidden("Principal.NotFound", "Principal not found.");

        var resourceArn = new ResourceArn(context.ResourceType.ToString(), context.TenantId.ToString(), context.ResourcePath);
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
                    if (statement.Actions.Any(a => AuthorizationHelper.IsActionMatched(context.RequiredPermission, a)) &&
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
                    if (statement.Actions.Any(a => AuthorizationHelper.IsActionMatched(context.RequiredPermission, a)) &&
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
}