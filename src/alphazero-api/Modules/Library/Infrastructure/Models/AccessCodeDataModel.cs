using System.Text.Json;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.Infrastructure.Models;

/// <summary>
/// EF Core persistence data model for the AccessCodes table.
/// Kept strictly separate from the persistence-ignorant AccessCode domain aggregate.
/// </summary>
public class AccessCodeDataModel : IDomainTenantOwned
{
    public Guid Id { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Guid? LibraryId { get; set; }
    public string StrategyId { get; set; } = string.Empty;
    public ResourceArn TargetResourceArn { get; set; } = default!;
    public JsonDocument Metadata { get; set; } = default!;
    public AccessCodeStatus Status { get; set; }
    public Guid? BatchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public Guid? RedeemedByUserId { get; set; }
    public string? VoidedReason { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
