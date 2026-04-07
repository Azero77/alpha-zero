using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using Amazon.Auth.AccessControlPolicy;
using ErrorOr;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AlphaZero.Modules.Identity.Domain.Models;

/// <summary>
/// A Principal is the actor within a specific Tenant.
/// It links a Global User to a Tenant and provides Authorization.
/// </summary>
public class Principal : TenantOwnedAggregate
{
    internal const string RegexForPrincipalScopeUrn = @"^(?<prefix>az):(?<service>[a-zA-z]+):(?<tenantId>[a-zA-Z0-9-]+):(?<path>[A-Za-z0-9\/\*\/\-\{\}]+)$"; 

    /// <summary>
    /// The unique Subject ID (sub) from AWS Cognito.
    /// </summary>
    public string IdentityId { get; private set; } = string.Empty;
    public PrincipalType PrincipalType { get; private set; }
    public string? PrincipalScopeUrn { get; private set; }
    private List<Policy> _inlinePolicies = new List<Policy>();
    public IReadOnlyCollection<Policy> InlinePolicies => _inlinePolicies.AsReadOnly();

    // EF Core Constructor
    protected Principal() { }

    private Principal(Guid id, string identityId, PrincipalType type, Guid tenantId, string principalScopeUrn) 
        : base(id, tenantId)
    {
        IdentityId = identityId;
        PrincipalType = type;
        PrincipalScopeUrn = principalScopeUrn;
    }
    public static ErrorOr<Principal> Create(Guid id, string identityId, PrincipalType type, Guid tenantId, string PrincipalScope)
    {
        var regex = new Regex(RegexForPrincipalScopeUrn,RegexOptions.Compiled); 
        var result = regex.Match(PrincipalScope);
        if (!result.Success)
            return Error.Validation("Principal scope is not valid");
        var path = result.Groups["path"].Value;
        if (!IsValidPath(path))
            return Error.Validation("Path is not valid");
        return new Principal(id, identityId, type, tenantId, PrincipalScope);
    }

    private static bool IsValidPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            // allow wildcard
            if (segment == "*")
                continue;

            // allow parameter
            if (segment.StartsWith("{") && segment.EndsWith("}"))
                continue;

            // normal segment
            if (!Regex.IsMatch(segment, @"^[a-zA-Z0-9\-]+$"))
                return false;
        }

        return true;
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


public class ManagedPolicy
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string PolicyName { get; init; } = string.Empty;
    public List<PolicyTemplateStatement> Statements { get; init; } = new List<PolicyTemplateStatement>();
    public Policy Build(string principalScope, Guid tenantId)
    {
        //for example az:tenantId:courses/2340-35434:*
        var returnedPolicy = new Policy(Id,Name,tenantId);
        foreach (var statement in Statements)
        {
            returnedPolicy.AddStatement(new PolicyStatement
            {
                Sid = statement.Sid,
                Effect = statement.Effect,
                Actions = statement.Actions,
                Resources = [principalScope],
                Condition = null
            });
        }

        return returnedPolicy;
    }
}

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