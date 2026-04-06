using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using Amazon.Auth.AccessControlPolicy;
using System.Text.Json;

namespace AlphaZero.Modules.Identity.Domain.Models;

/// <summary>
/// A Principal is the actor within a specific Tenant.
/// It links a Global User to a Tenant and provides Authorization.
/// </summary>
public class Principal : TenantOwnedAggregate
{
    public Guid UserId { get; private set; }
    public PrincipalType PrincipalType { get; private set; }
    public string? PrincipalScopeUrn { get; private set; } //for setting scope for the managed policies to be attached to this principal, e.g. "az:tenantId:courses/courseId/*" so all actions in course id is approved as long they have the managed policies
    // In-memory list for domain logic, but persisted as JSONB in Postgres
    private List<PolicyStatement> _inlinePolicies = new();
    public IReadOnlyCollection<PolicyStatement> InlinePolicies => _inlinePolicies.AsReadOnly();

    // EF Core Constructor
    protected Principal() { }

    public Principal(Guid id, Guid userId, PrincipalType type, Guid tenantId) 
        : base(id, tenantId)
    {
        UserId = userId;
        PrincipalType = type;
    }

    public void AddInlinePolicy(PolicyStatement statement)
    {
        _inlinePolicies.Add(statement);
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

    public void AddStatement(PolicyStatement statement)
    {
        _statements.Add(statement);
    }
}
/// <summary>
/// Already Configured Policies to be referenced for a resource or tenant,
/// these policies will be attached to Principals and will have lower precedence than inline policies but higher precedence than other attached policies.
/// They are always Allow and Deny will be handled by inline policies with higher precedence
/// </summary>
public class AttachedPolicy
{
    public string PolicyName { get; private set; } = string.Empty;
    public List<string> Actions { get; private set; } = new();
    public PolicyStatement BuildPolicy(List<string> resourceUrns,Guid tenantId)
    {
        return new PolicyStatement()
        {
            Actions = Actions,
            Effective = true, // Attached policies are always Allow, Deny will be handled by inline policies with higher precedence
            Resources = resourceUrns,
            StatementNameId = PolicyName,
        };
    }
}
public record Resource(Guid ResourceId, ResourceType ResourceType);


public enum ResourceType
{
    Course,
    Subject,
    User,
    Video
    // Add more resource types as needed
}
public enum PrincipalType
{
    User,
    Role
}

public static class ResourceHelper
{
    public static string GetUrn(Guid resourceId, ResourceType resourceType, Guid tenantId)
    {
        return $"az:{tenantId}:{resourceType.ToString().ToLower()}/{resourceId}";
    }
}