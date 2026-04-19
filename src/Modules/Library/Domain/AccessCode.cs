using System.Text.Json;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Library.Domain;

public class AccessCode : AggregateRoot, IDomainTenantOwned
{
    public string CodeHash { get; private set; } = default!;
    public Guid TenantId { get; private set; }
    public Guid? LibraryId { get; private set; }
    public string StrategyId { get; private set; } = default!;
    public ResourceArn TargetResourceArn { get; private set; } = default!;
    public JsonDocument Metadata { get; private set; } = default!;
    public AccessCodeStatus Status { get; private set; }
    public Guid? BatchId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RedeemedAt { get; private set; }
    public Guid? RedeemedByUserId { get; private set; }

    private AccessCode() { }

    private AccessCode(
        Guid id,
        string codeHash,
        Guid tenantId,
        Guid? libraryId,
        string strategyId,
        ResourceArn targetResourceArn,
        JsonDocument metadata,
        AccessCodeStatus status,
        Guid? batchId,
        DateTime createdAt) : base(id)
    {
        CodeHash = codeHash;
        TenantId = tenantId;
        LibraryId = libraryId;
        StrategyId = strategyId;
        TargetResourceArn = targetResourceArn;
        Metadata = metadata;
        Status = status;
        BatchId = batchId;
        CreatedAt = createdAt;
    }

    public static AccessCode Mint(
        string codeHash, 
        Guid tenantId, 
        Guid? libraryId,
        string strategyId, 
        ResourceArn targetResourceArn, 
        JsonDocument metadata,
        Guid? batchId = null)
    {
        return new AccessCode(
            Guid.NewGuid(),
            codeHash,
            tenantId,
            libraryId,
            strategyId,
            targetResourceArn,
            metadata,
            AccessCodeStatus.Minted,
            batchId,
            DateTime.UtcNow);
    }

    public ErrorOr<Success> Redeem(Guid userId)
    {
        if (Status != AccessCodeStatus.Minted && Status != AccessCodeStatus.Distributed)
        {
            return Error.Conflict("AccessCode.InvalidStatus", $"Code cannot be redeemed. Current status: {Status}");
        }

        Status = AccessCodeStatus.Redeemed;
        RedeemedAt = DateTime.UtcNow;
        RedeemedByUserId = userId;
        
        return Result.Success;
    }

    public ErrorOr<Success> Distribute()
    {
        if (Status != AccessCodeStatus.Minted)
        {
            return Error.Conflict("AccessCode.InvalidStatus", "Only Minted codes can be distributed.");
        }

        Status = AccessCodeStatus.Distributed;
        return Result.Success;
    }

    public ErrorOr<Success> Void(string reason)
    {
        if (Status == AccessCodeStatus.Voided)
        {
            return Error.Conflict("AccessCode.AlreadyVoided", "Code is already voided.");
        }

        var oldStatus = Status;
        Status = AccessCodeStatus.Voided;

        // If it was already redeemed, we must trigger a revocation event
        if (oldStatus == AccessCodeStatus.Redeemed && RedeemedByUserId.HasValue)
        {
            AddDomainEvent(new AccessCodeVoidedDomainEvent(Id, RedeemedByUserId.Value, TargetResourceArn));
        }

        return Result.Success;
    }
}

public class AccessCodeVoidedDomainEvent(Guid AccessCodeId, Guid UserId, ResourceArn Resource) : DomainEvent()
{
    public Guid AccessCodeId { get; } = AccessCodeId;
    public Guid UserId { get; } = UserId;
    public ResourceArn Resource { get; } = Resource;
}
