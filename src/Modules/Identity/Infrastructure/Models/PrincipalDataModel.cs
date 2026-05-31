using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Identity.Infrastructure.Models;

internal class PrincipalDataModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PrincipalType PrincipalType { get; set; }
    public string? PrincipalScopePattern { get; set; }
    public Guid TenantId { get; set; }

    // Navigation for Many-to-Many
    public List<ManagedPolicy> ManagedPolicies { get; set; } = new();

    // Field for JSONB serialization
    public List<InlinePolicy> InlinePolicies { get; set; } = new();
}
