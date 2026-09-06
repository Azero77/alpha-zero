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

    private readonly List<IPolicy> _policies = new();
    public IReadOnlyCollection<IPolicy> Policies => _policies.AsReadOnly();
    public bool IsManaged => PrincipalScope is null;
    public bool IsGlobal => TenantId == Guid.Empty;
    private Principal() { } 

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
        if (!_policies.Any(p => p is Entity e && policy is Entity pe && e.Id == pe.Id))
        {
            _policies.Add(policy);
        }
    }

    public void RemovePolicy(Guid policyId)
    {
        var policy = _policies.FirstOrDefault(p => p is Entity e && e.Id == policyId);
        if (policy is not null)
        {
            _policies.Remove(policy);
        }
    }

    /// <summary>
    /// For Repository use only - hydrates the state from the data model
    /// </summary>
    public void LoadPolicies(IEnumerable<IPolicy> policies)
    {
        _policies.Clear();
        _policies.AddRange(policies);
    }
}


public class PrincipalLoginService(IPasswordHasher passwordHasher)
{
    
    public ErrorOr<Success> Login(Principal principal, string password)
    {
        if (principal.IsManaged)
        {
            return Error.Unexpected("Auth.ManagedPrincipal", "Managed principals cannot be logged in with.");
        }
        if(!passwordHasher.VerifyPassword(password, principal.PasswordHash))
        {
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid username or password.");
        }
        return Result.Success;
    }
}