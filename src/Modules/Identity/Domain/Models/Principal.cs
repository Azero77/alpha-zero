using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models;

public class Principal : PrincipalTemplate, IDomainTenantOwned
{
    public string TenantUserId { get; private set; } = string.Empty;
    
    // Boundary: Explicitly using ResourcePattern
    public ResourcePattern? PrincipalScope { get; private set; }
    
    private List<Policy> _inlinePolicies = new List<Policy>();
    public IReadOnlyCollection<Policy> InlinePolicies => _inlinePolicies.AsReadOnly();
    
    public ResourceType? ScopeResourceType { get; private set; }
    public Guid? ResourceId { get; private set; }

    public Guid TenantId { get; private set; }
    public string? PrincipalScopeUrn => PrincipalScope?.Value;

    private Principal(Guid id, string identityId, PrincipalType type, Guid tenantId, ResourcePattern? scope, string name, Guid? resourceId = null, ResourceType? scopeResourceType = null) 
        : base(id, name, type)
    {
        TenantUserId = identityId;
        PrincipalScope = scope;
        ResourceId = resourceId;
        ScopeResourceType = scopeResourceType;
        TenantId = tenantId;
    }

    public static ErrorOr<Principal> Create(Guid id, string identityId, PrincipalType type, Guid tenantId, string? principalScope, string name, Guid? resourceId = null, ResourceType? scopeResourceType = null)
    {
        if ((principalScope is null && resourceId is not null) || (principalScope is not null && resourceId is null))
            return Error.Validation("Principal.InvalidScope", "Principal scope pattern and resource ID must be either both null or both non-null.");

        
        if (principalScope is not null)
        {
            ErrorOr<ResourcePattern> pattern = ResourcePattern.Create(principalScope);
            if (pattern.IsError)
                return pattern.Errors;

            return new Principal(id, identityId, type, tenantId, pattern.Value, name, resourceId, scopeResourceType);
            
        }

        return new Principal(id, identityId, type, tenantId, null, name, resourceId, scopeResourceType);
    }

    public void AddInlinePolicy(Policy policy)
    {
        if (!_inlinePolicies.Any(p => p.Id == policy.Id))
            _inlinePolicies.Add(policy);
    }
    public void RemoveInlinePolicy(Guid policyId)
    {
        var policy = _inlinePolicies.FirstOrDefault(p => p.Id == policyId);
        if (policy != null)
        {
            _inlinePolicies.Remove(policy);
        }
    }
}

public class Policy : TenantOwnedAggregate
{
    public string Name { get; private set; } = string.Empty;

    private List<PolicyStatement> _statements = new();
    public IReadOnlyCollection<PolicyStatement> Statements => _statements.AsReadOnly();

    // EF Core Constructor
    protected Policy() { }

    public Policy(Guid id, string name, Guid tenantId) : base(id, tenantId)
    {
        Name = name;
    }

    public ErrorOr<Success> AddStatement(PolicyStatement statement)
    {
        if (_statements.Any(s => s.Sid == statement.Sid))
            return Error.Conflict("Policy.Conflict", $"Statement with Sid {statement.Sid} already exists in the policy.");
        _statements.Add(statement);
        return Result.Success;
    }
}
public record Resource(Guid ResourceId, ResourceType ResourceType);


public class ManagedPolicy : Entity
{

    public ManagedPolicy(Guid id, string name, List<PolicyTemplateStatement> statements) : base(id)
    {
        Name = name;
        Statements = statements;
    }

    public string Name { get; init; } = string.Empty;
    public List<PolicyTemplateStatement> Statements { get; init; } = new List<PolicyTemplateStatement>();
    public Policy Build(string principalScope, Guid tenantId)
    {
        //for example az:tenantId:courses/2340-35434:*
        var returnedPolicy = new Policy(Id, Name, tenantId);
        foreach (var statement in Statements)
        {
            returnedPolicy.AddStatement(new PolicyStatement(statement.Sid, statement.Actions, statement.Effect, new List<ResourcePattern> { ResourcePattern.Create(principalScope).Value }));
        }

        return returnedPolicy;
    }


}
public enum PrincipalType
{
    User,
    Role
}
