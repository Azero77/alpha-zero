using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
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

    public async Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        var strategy = _strategies.FirstOrDefault(s => s.Method.ToString().Equals(context.AuthenticationMethod, StringComparison.OrdinalIgnoreCase));
        
        if (strategy == null)
            return Error.Forbidden("Identity.Auth", $"No strategy found for auth method: {context.AuthenticationMethod}");

        return await strategy.Authorize(context);
    }
}

public interface IPolicyEvaluationEngine
{
    Task<ErrorOr<Success>> Evaluate(IEnumerable<PolicyStatement> statements, AuthorizationContext context, ResourceArn targetArn);
}

public class PolicyEvaluationEngine(ConditionEvaluatorService conditionEvaluator) : IPolicyEvaluationEngine
{
    public async Task<ErrorOr<Success>> Evaluate(IEnumerable<PolicyStatement> statements, AuthorizationContext context, ResourceArn targetArn)
    {
        bool isAllowed = false;
        List<Error> conditionErrors = new();

        foreach (var statement in statements)
        {
            var matchResult = await AuthorizationHelper.IsStatementMatch(statement, context.RequiredPermission, targetArn, conditionEvaluator, context);
            
            if (matchResult.IsError)
            {
                // If it's a DENY statement and condition fails, it's NOT a deny.
                // If it's an ALLOW statement and condition fails, we record why it failed.
                if (statement.Effect)
                {
                    conditionErrors.AddRange(matchResult.Errors);
                }
                continue;
            }

            if (matchResult.Value)
            {
                if (!statement.Effect) 
                    return Error.Forbidden("Access.Denied", "Explicit deny.");
                
                isAllowed = true;
            }
        }

        if (isAllowed) return Result.Success;

        // If we have specific condition errors (like Main Device mismatch), return them.
        if (conditionErrors.Any()) return conditionErrors;

        return Error.Forbidden("Access.Denied", "Implicit deny.");
    }
}

public interface IAuthorizationStrategy
{
    AuthenticationMethod Method { get; }
    Task<ErrorOr<Success>> Authorize(AuthorizationContext context);
}

public class TenantUserAuthorizationStrategy : IAuthorizationStrategy
{
    private readonly ITenantUserPrincpialAssignmentRepository _assignmentRepository;
    private readonly IPolicyEvaluationEngine _evaluationEngine;

    public TenantUserAuthorizationStrategy(
        ITenantUserPrincpialAssignmentRepository assignmentRepository,
        IPolicyEvaluationEngine evaluationEngine)
    {
        _assignmentRepository = assignmentRepository;
        _evaluationEngine = evaluationEngine;
    }

    public AuthenticationMethod Method => AuthenticationMethod.TenantUser;

    public async Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        var targetArnResult = ResourceArn.Create(context.ResourceType, context.TenantId.ToString(), context.ResourcePath);
        if (targetArnResult.IsError) return Error.Forbidden("Resource.Invalid");
        var targetArn = targetArnResult.Value;

        var assignment = await _assignmentRepository.Get(context.Id, targetArn.ToString());
        if (assignment == null) return Error.Forbidden("Access.Denied", "No matching assignment found.");

        var assignmentScope = assignment.Resource.ToString() + "/*";
        var statements = new List<PolicyStatement>();

        foreach (var policy in assignment.Policies)
        {
            var statementsResult = policy.GetPolicyStatements(assignmentScope, assignment.TenantId);
            if (statementsResult.IsError) return statementsResult.Errors;
            statements.AddRange(statementsResult.Value);
        }

        return await _evaluationEngine.Evaluate(statements, context, targetArn);
    }
}

public class PrincipalUserAuthorizationStrategy : IAuthorizationStrategy
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly IPolicyEvaluationEngine _evaluationEngine;

    public PrincipalUserAuthorizationStrategy(
        IPrincipalRepository principalRepository,
        IPolicyEvaluationEngine evaluationEngine)
    {
        _principalRepository = principalRepository;
        _evaluationEngine = evaluationEngine;
    }

    public AuthenticationMethod Method => AuthenticationMethod.Principal;

    public async Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        var principal = await _principalRepository.GetById(context.Id);
        if (principal is null) return Error.Forbidden("Principal.NotFound");

        var targetArnResult = ResourceArn.Create(context.ResourceType, context.TenantId.ToString(), context.ResourcePath);
        if (targetArnResult.IsError) return Error.Forbidden("Resource.Invalid");
        var targetArn = targetArnResult.Value;

        if (context.TenantId is null) return Error.Forbidden("Access.Denied", "TenantId is required.");

        var activeScope = principal.PrincipalScope?.Value ?? "az:*";
        var statements = new List<PolicyStatement>();

        foreach (var policy in principal.Policies)
        {
            var statementsResult = policy.GetPolicyStatements(activeScope, context.TenantId.Value);
            if (statementsResult.IsError) return statementsResult.Errors;
            statements.AddRange(statementsResult.Value);
        }

        return await _evaluationEngine.Evaluate(statements, context, targetArn);
    }
}

public static class AuthorizationHelper
{
    public static bool IsActionMatched(string requiredPermission, string givenAction)
    {
        // 1. Exact Match or Global Wildcard
        if (givenAction == "*" || givenAction == "*:*" || requiredPermission.Equals(givenAction, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. Handle Segmented Wildcards (service:action)
        var requiredParts = requiredPermission.Split(':');
        var givenParts = givenAction.Split(':');

        if (requiredParts.Length != 2 || givenParts.Length != 2)
        {
            // Fallback to simple trailing wildcard if not in service:action format
            if (givenAction.EndsWith("*"))
            {
                var prefix = givenAction.Substring(0, givenAction.Length - 1);
                return requiredPermission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        // Match Service Part
        bool serviceMatch = givenParts[0] == "*" || string.Equals(givenParts[0], requiredParts[0], StringComparison.OrdinalIgnoreCase);
        
        // Match Action Part
        bool actionMatch = false;
        if (givenParts[1] == "*" || givenParts[1].EndsWith("*") && requiredParts[1].StartsWith(givenParts[1].Substring(0, givenParts[1].Length - 1), StringComparison.OrdinalIgnoreCase))
        {
            actionMatch = true;
        }
        else
        {
            actionMatch = string.Equals(givenParts[1], requiredParts[1], StringComparison.OrdinalIgnoreCase);
        }

        return serviceMatch && actionMatch;
    }

    public static async Task<ErrorOr<bool>> IsStatementMatch(PolicyStatement statement, string requiredPermission, ResourceArn targetArn, ConditionEvaluatorService conditionEvaluator, AuthorizationContext context)
    {
        bool baseMatch = statement.Actions.Any(a => IsActionMatched(requiredPermission, a)) &&
                         statement.Resources.Any(r => r.IsMatch(targetArn));

        if (!baseMatch) return false;

        if (statement.Condition is not null)
        {
            var conditionResult = await conditionEvaluator.Evaluate(statement.Condition, context);
            if (conditionResult.IsError)
            {
                return conditionResult.Errors;
            }
        }
        return true;
    }
}
