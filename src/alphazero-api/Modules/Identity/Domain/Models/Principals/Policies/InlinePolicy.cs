using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;

public class InlinePolicy : Entity, IPolicy, IDomainTenantOwned
{
    public PolicyType Type => PolicyType.Inline;
    public string Name { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public bool IsGlobal => TenantId == Guid.Empty;

    private List<PolicyStatement> _statements = new List<PolicyStatement>();
    public IReadOnlyCollection<PolicyStatement> Statements => _statements.AsReadOnly();

    private InlinePolicy() { } // EF and JSON

    public InlinePolicy(Guid id, string name, Guid tenantId) : base(id)
    {
        Name = name;
        TenantId = tenantId;
    }

    public ErrorOr<IReadOnlyCollection<PolicyStatement>> GetPolicyStatements(string? scope, Guid tenantId)
        => Statements.ToErrorOr();

    public ErrorOr<Success> AddStatement(PolicyStatement statement)
    {
        if (_statements.Any(s => s.Sid == statement.Sid))
            return Error.Conflict("Policy.Conflict", $"Statement with Sid {statement.Sid} already exists in the policy.");
        
        _statements.Add(statement);
        return Result.Success;
    }
}
