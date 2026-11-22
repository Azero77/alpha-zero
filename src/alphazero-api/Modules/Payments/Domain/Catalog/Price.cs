using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Catalog;

public sealed class Price : AggregateRoot
{
    private Price() { }

    private Price(Guid id, Guid tenantId, Guid productId, Money amount, Guid createdByUserId,
        bool isActive, DateTime createdAt, DateTime? archivedAt) : base(id)
    {
        TenantId = tenantId;
        ProductId = productId;
        Amount = amount;
        CreatedByUserId = createdByUserId;
        IsActive = isActive;
        CreatedAt = createdAt;
        ArchivedAt = archivedAt;
    }

    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    public PriceStatus? ExpectedPreviousStatus { get; private set; }

    public static ErrorOr<Price> Create(Guid id, Guid tenantId, Guid productId, Money amount,
        Guid createdByUserId, DateTime createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || productId == Guid.Empty || createdByUserId == Guid.Empty)
            return Error.Validation("Price.Identity", "Price identity and creator are required.");
        if (amount.Amount <= 0)
            return Error.Validation("Price.Amount", "Price amount must be positive.");
        var price = new Price(id, tenantId, productId, amount, createdByUserId, true, createdAt, null);
        price.AddDomainEvent(new PriceCreatedDomainEvent(id, tenantId, productId, amount, createdAt));
        return price;
    }

    public static Price Reconstitute(Guid id, Guid tenantId, Guid productId, Money amount,
        Guid createdByUserId, bool isActive, DateTime createdAt, DateTime? archivedAt) =>
        new(id, tenantId, productId, amount, createdByUserId, isActive, createdAt, archivedAt);

    public ErrorOr<Success> Archive(DateTime archivedAt, bool hasActiveCheckout = false, bool hasPaidPurchase = false)
    {
        if (hasActiveCheckout || hasPaidPurchase)
            return Error.Conflict("Price.InUse", "A Price locked by a Pending Checkout Session or Paid Purchase cannot be archived.");
        if (!IsActive)
            return Error.Conflict("Price.AlreadyArchived", "An archived Price cannot be archived again.");
        ExpectedPreviousStatus = PriceStatus.Active;
        IsActive = false;
        ArchivedAt = archivedAt;
        AddDomainEvent(new PriceArchivedDomainEvent(Id, archivedAt));
        return Result.Success;
    }
}

public enum PriceStatus { Active, Archived }

public sealed record PriceCreatedDomainEvent(Guid PriceId, Guid TenantId, Guid ProductId, Money Amount, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}

public sealed record PriceArchivedDomainEvent(Guid PriceId, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}
