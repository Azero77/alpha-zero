using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Authorization;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models;

/// <summary>
/// A Blueprint for permissions. Represents a Global Role (e.g., Student, Teacher).
/// It holds ManagedPolicies that are shared by everyone assigned to this template.
/// </summary>
public class PrincipalTemplate : AggregateRoot
{
    public string Name { get; protected set; } = string.Empty;
    public PrincipalType PrincipalType { get; protected set; }

    // Policies shared by all instances of this template
    protected readonly List<ManagedPolicy> _managedPolicies = new();
    public IReadOnlyCollection<ManagedPolicy> ManagedPolicies => _managedPolicies.AsReadOnly();

    protected PrincipalTemplate() { } // EF Core

    protected PrincipalTemplate(Guid id, string name, PrincipalType type) : base(id)
    {
        Name = name;
        PrincipalType = type;
    }

    public static ErrorOr<PrincipalTemplate> Create(Guid id, string name, PrincipalType type)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Template.Name", "Template name is required.");

        return new PrincipalTemplate(id, name, type);
    }

    public void AttachManagedPolicy(ManagedPolicy policy)
    {
        if (!_managedPolicies.Any(p => p.Id == policy.Id))
            _managedPolicies.Add(policy);
    }

    public void DetachManagedPolicy(Guid policyId)
    {
        var policy = _managedPolicies.FirstOrDefault(p => p.Id == policyId);
        if (policy != null) _managedPolicies.Remove(policy);
    }
}
