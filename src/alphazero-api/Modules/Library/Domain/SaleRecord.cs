using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.Domain;

/// <summary>
/// Represents an immutable point-of-sale record for an access code sale.
/// Invariant I-MONEY: sold counts and revenue derive exclusively from SaleRecord rows.
/// </summary>
public class SaleRecord : Entity, IDomainTenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid LibraryId { get; private set; }
    public Guid AccessCodeId { get; private set; }
    public Guid SoldByUserId { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "USD";
    public DateTime SoldAt { get; private set; }

    private SaleRecord() { }

    private SaleRecord(
        Guid id,
        Guid tenantId,
        Guid libraryId,
        Guid accessCodeId,
        Guid soldByUserId,
        decimal price,
        string currency,
        DateTime soldAt) : base(id)
    {
        TenantId = tenantId;
        LibraryId = libraryId;
        AccessCodeId = accessCodeId;
        SoldByUserId = soldByUserId;
        Price = price;
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.ToUpperInvariant();
        SoldAt = soldAt;
    }

    public static SaleRecord Create(
        Guid tenantId,
        Guid libraryId,
        Guid accessCodeId,
        Guid soldByUserId,
        decimal price,
        string currency = "USD",
        DateTime? soldAt = null)
    {
        return new SaleRecord(
            Guid.NewGuid(),
            tenantId,
            libraryId,
            accessCodeId,
            soldByUserId,
            price,
            currency,
            soldAt ?? DateTime.UtcNow);
    }
}
