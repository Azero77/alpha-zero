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
    public string? PrincipalScopeUrn { get; private set; }
    private List<Policy> _inlinePolicies = new List<Policy>();
    public IReadOnlyCollection<Policy> InlinePolicies => _inlinePolicies.AsReadOnly();

    // EF Core Constructor
    protected Principal() { }

    public Principal(Guid id, Guid userId, PrincipalType type, Guid tenantId) 
        : base(id, tenantId)
    {
        UserId = userId;
        PrincipalType = type;
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