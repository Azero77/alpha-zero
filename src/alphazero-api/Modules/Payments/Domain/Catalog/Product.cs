using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Catalog;

public sealed class Product : AggregateRoot
{
    private Product() { }

    private Product(Guid id, Guid tenantId, string itemType, Guid itemId, Guid? parentContainerId,
        string targetResourceArn, string title, SourceAvailability sourceAvailability, long sourceVersion,
        DateTime createdAt, DateTime? updatedAt) : base(id)
    {
        TenantId = tenantId;
        ItemType = itemType;
        ItemId = itemId;
        ParentContainerId = parentContainerId;
        TargetResourceArn = targetResourceArn;
        Title = title;
        SourceAvailability = sourceAvailability;
        SourceVersion = sourceVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid TenantId { get; private set; }
    public string ItemType { get; private set; } = null!;
    public Guid ItemId { get; private set; }
    public Guid? ParentContainerId { get; private set; }
    public string TargetResourceArn { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public SourceAvailability SourceAvailability { get; private set; }
    public long SourceVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static ErrorOr<Product> Create(Guid id, Guid tenantId, string itemType, Guid itemId,
        Guid? parentContainerId, string targetResourceArn, string title, SourceAvailability availability, long sourceVersion,
        DateTime occurredAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || itemId == Guid.Empty)
            return Error.Validation("Product.Identity", "Product identity fields are required.");
        if (string.IsNullOrWhiteSpace(itemType) || itemType.Length > 32)
            return Error.Validation("Product.ItemType", "A valid item type is required.");
        if (itemType == "CoursePlan" && parentContainerId is null)
            return Error.Validation("Product.ParentContainer", "Course Plan products require their parent Course id.");
        if (string.IsNullOrWhiteSpace(targetResourceArn) || targetResourceArn.Length > 512)
            return Error.Validation("Product.TargetResourceArn", "A valid target resource ARN is required.");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 255)
            return Error.Validation("Product.Title", "A title of at most 255 characters is required.");
        if (sourceVersion < 0)
            return Error.Validation("Product.SourceVersion", "Source version cannot be negative.");

        var product = new Product(id, tenantId, itemType, itemId, parentContainerId, targetResourceArn,
            title, availability, sourceVersion, occurredAt, null);
        product.AddDomainEvent(new ProductMirrorUpdatedDomainEvent(id, sourceVersion, availability, occurredAt));
        return product;
    }

    public static Product Reconstitute(Guid id, Guid tenantId, string itemType, Guid itemId,
        Guid? parentContainerId, string targetResourceArn, string title, SourceAvailability availability, long sourceVersion,
        DateTime createdAt, DateTime? updatedAt) => new(id, tenantId, itemType, itemId, parentContainerId,
        targetResourceArn, title, availability, sourceVersion, createdAt, updatedAt);

    public ErrorOr<bool> ApplyMirrorUpdate(long sourceVersion, string title, SourceAvailability availability,
        string targetResourceArn, DateTime occurredAt)
    {
        if (sourceVersion <= SourceVersion)
            return false;
        if (availability == SourceAvailability.Available && !TargetResourceArn.Equals(targetResourceArn, StringComparison.Ordinal))
            return Error.Conflict("Product.IdentityChanged", "A source Course Plan cannot change its target resource identity.");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 255)
            return Error.Validation("Product.Title", "A title of at most 255 characters is required.");
        if (string.IsNullOrWhiteSpace(targetResourceArn) || targetResourceArn.Length > 512)
            return Error.Validation("Product.TargetResourceArn", "A valid target resource ARN is required.");

        Title = title;
        SourceAvailability = availability;
        TargetResourceArn = targetResourceArn;
        SourceVersion = sourceVersion;
        UpdatedAt = occurredAt;
        AddDomainEvent(new ProductMirrorUpdatedDomainEvent(Id, sourceVersion, availability, occurredAt));
        return true;
    }
}

public sealed record ProductMirrorUpdatedDomainEvent(Guid ProductId, long SourceVersion,
    SourceAvailability Availability, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}
