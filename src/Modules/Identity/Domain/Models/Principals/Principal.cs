using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models.Principals;

public enum PrincipalType
{
    User,
    Role
}

public class Principal : AggregateRoot, IDomainTenantOwned
{
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PrincipalType PrincipalType { get; private set; }
    public ResourcePattern? PrincipalScope { get; private set; }
    public Guid TenantId { get; private set; }

    private List<IPolicy> _policies = new List<IPolicy>();
    public IReadOnlyCollection<IPolicy> Policies => _policies.AsReadOnly();

    private Principal() { } // EF and JSON

    private Principal(Guid id, string username, string passwordHash, string name, PrincipalType type, ResourcePattern? principalScope, Guid tenantId) : base(id)
    {
        Username = username;
        PasswordHash = passwordHash;
        Name = name;
        PrincipalType = type;
        PrincipalScope = principalScope;
        TenantId = tenantId;
    }

    public static ErrorOr<Principal> Create(Guid id, string username, string passwordHash, string name, PrincipalType type, string? principalScope, Guid tenantId)
    {
        ResourcePattern? pattern = null;
        if (principalScope is not null)
        {
            var patternResult = ResourcePattern.Create(principalScope);
            if (patternResult.IsError)
                return patternResult.Errors;
            
            pattern = patternResult.Value;
        }

        return new Principal(id, username, passwordHash, name, type, pattern, tenantId);
    }

    public void AddPolicy(IPolicy policy)
    {
        if (!_policies.Contains(policy))
        {
            _policies.Add(policy);
        }
    }
}
