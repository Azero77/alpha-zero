using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;

public interface IPolicy
{
    Guid Id { get; }
    string Name { get; }
    PolicyType Type { get; }
    ErrorOr<IReadOnlyCollection<PolicyStatement>> GetPolicyStatements(string? scope, Guid tenantId);
}

public enum PolicyType
{
    Inline,
    Managed
}
