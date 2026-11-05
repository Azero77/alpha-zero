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
    public string? VoidedReason { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// Tracks the status of the code prior to the most recent in-memory transition.
    /// Used by persistence repositories to enforce conditional tripwire updates.
    /// </summary>
    public AccessCodeStatus? ExpectedPreviousStatus { get; private set; }

    private AccessCodeState _state = default!;

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
        DateTime createdAt,
        DateTime? redeemedAt = null,
        Guid? redeemedByUserId = null,
        string? voidedReason = null,
        DateTime? expiresAt = null) : base(id)
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
        RedeemedAt = redeemedAt;
        RedeemedByUserId = redeemedByUserId;
        VoidedReason = voidedReason;
        ExpiresAt = expiresAt?.ToUniversalTime();
        _state = AccessCodeState.Create(status);
    }

    public static AccessCode Mint(
        string codeHash,
        Guid tenantId,
        Guid? libraryId,
        string strategyId,
        ResourceArn targetResourceArn,
        JsonDocument metadata,
        Guid? batchId = null,
        DateTime? expiresAt = null)
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
            DateTime.UtcNow,
            expiresAt: expiresAt);
    }

    public static AccessCode Reconstitute(
        Guid id,
        string codeHash,
        Guid tenantId,
        Guid? libraryId,
        string strategyId,
        ResourceArn targetResourceArn,
        JsonDocument metadata,
        AccessCodeStatus status,
        Guid? batchId,
        DateTime createdAt,
        DateTime? redeemedAt,
        Guid? redeemedByUserId,
        string? voidedReason,
        DateTime? expiresAt)
    {
        return new AccessCode(
            id,
            codeHash,
            tenantId,
            libraryId,
            strategyId,
            targetResourceArn,
            metadata,
            status,
            batchId,
            createdAt,
            redeemedAt,
            redeemedByUserId,
            voidedReason,
            expiresAt);
    }

    public ErrorOr<Success> Distribute()
    {
        return _state.Distribute(this);
    }

    public ErrorOr<Success> Sell(Guid libraryId, Guid soldByUserId, decimal price, string currency, DateTime? now = null)
    {
        return _state.Sell(this, libraryId, soldByUserId, price, currency, now);
    }

    public ErrorOr<Success> Redeem(Guid userId, DateTime? now = null)
    {
        return _state.Redeem(this, userId, now);
    }

    public ErrorOr<Success> Void(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("AccessCode.EmptyReason", "A void reason is required.");
        }

        return _state.Void(this, reason);
    }

    public ErrorOr<Success> Expire(DateTime? now = null)
    {
        return _state.Expire(this, now);
    }

    private void TransitionTo(AccessCodeStatus newStatus, AccessCodeState newState)
    {
        ExpectedPreviousStatus = Status;
        Status = newStatus;
        _state = newState;
    }

    #region State Pattern Classes

    private abstract class AccessCodeState
    {
        public abstract AccessCodeStatus Status { get; }

        public static AccessCodeState Create(AccessCodeStatus status) => status switch
        {
            AccessCodeStatus.Minted => new MintedState(),
            AccessCodeStatus.Distributed => new DistributedState(),
            AccessCodeStatus.Sold => new SoldState(),
            AccessCodeStatus.Redeemed => new RedeemedState(),
            AccessCodeStatus.Voided => new VoidedState(),
            AccessCodeStatus.Expired => new ExpiredState(),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown access code status.")
        };

        public virtual ErrorOr<Success> Distribute(AccessCode code)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code in {Status} status cannot be distributed.");

        public virtual ErrorOr<Success> Sell(AccessCode code, Guid libraryId, Guid soldByUserId, decimal price, string currency, DateTime? now)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code in {Status} status cannot be sold.");

        public virtual ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code cannot be redeemed. Current status: {Status}");

        public virtual ErrorOr<Success> Void(AccessCode code, string reason)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code in {Status} status cannot be voided.");

        public virtual ErrorOr<Success> Expire(AccessCode code, DateTime? now)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code in {Status} status cannot be expired.");
    }

    private sealed class MintedState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Minted;

        public override ErrorOr<Success> Distribute(AccessCode code)
        {
            code.TransitionTo(AccessCodeStatus.Distributed, new DistributedState());
            code.AddDomainEvent(new AccessCodeDistributedDomainEvent(code.Id, code.LibraryId, code.BatchId, DateTime.UtcNow));
            return Result.Success;
        }

        public override ErrorOr<Success> Void(AccessCode code, string reason)
        {
            code.VoidedReason = reason;
            code.TransitionTo(AccessCodeStatus.Voided, new VoidedState());
            code.AddDomainEvent(new AccessCodeVoidedDomainEvent(code.Id, null, code.TargetResourceArn, reason));
            return Result.Success;
        }

        public override ErrorOr<Success> Expire(AccessCode code, DateTime? now)
        {
            var expiredAt = now ?? DateTime.UtcNow;
            code.TransitionTo(AccessCodeStatus.Expired, new ExpiredState());
            code.AddDomainEvent(new AccessCodeExpiredDomainEvent(code.Id, expiredAt));
            return Result.Success;
        }
    }

    private sealed class DistributedState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Distributed;

        public override ErrorOr<Success> Sell(AccessCode code, Guid libraryId, Guid soldByUserId, decimal price, string currency, DateTime? now)
        {
            var currentTime = now ?? DateTime.UtcNow;
            if (code.ExpiresAt.HasValue && currentTime >= code.ExpiresAt.Value)
            {
                return Error.Conflict("AccessCode.Expired", "This access code has expired.");
            }

            code.TransitionTo(AccessCodeStatus.Sold, new SoldState());
            code.AddDomainEvent(new AccessCodeSoldDomainEvent(code.Id, code.TenantId, libraryId, soldByUserId, price, currency, currentTime));
            return Result.Success;
        }

        public override ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now)
        {
            var currentTime = now ?? DateTime.UtcNow;
            if (code.ExpiresAt.HasValue && currentTime >= code.ExpiresAt.Value)
            {
                return Error.Conflict("AccessCode.Expired", "This access code has expired.");
            }

            code.RedeemedAt = currentTime;
            code.RedeemedByUserId = userId;
            code.TransitionTo(AccessCodeStatus.Redeemed, new RedeemedState());
            code.AddDomainEvent(new AccessCodeRedeemedDomainEvent(code.Id, userId, code.TargetResourceArn, currentTime));
            return Result.Success;
        }

        public override ErrorOr<Success> Void(AccessCode code, string reason)
        {
            code.VoidedReason = reason;
            code.TransitionTo(AccessCodeStatus.Voided, new VoidedState());
            code.AddDomainEvent(new AccessCodeVoidedDomainEvent(code.Id, null, code.TargetResourceArn, reason));
            return Result.Success;
        }

        public override ErrorOr<Success> Expire(AccessCode code, DateTime? now)
        {
            var expiredAt = now ?? DateTime.UtcNow;
            code.TransitionTo(AccessCodeStatus.Expired, new ExpiredState());
            code.AddDomainEvent(new AccessCodeExpiredDomainEvent(code.Id, expiredAt));
            return Result.Success;
        }
    }

    private sealed class SoldState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Sold;

        public override ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now)
        {
            var currentTime = now ?? DateTime.UtcNow;
            if (code.ExpiresAt.HasValue && currentTime >= code.ExpiresAt.Value)
            {
                return Error.Conflict("AccessCode.Expired", "This access code has expired.");
            }

            code.RedeemedAt = currentTime;
            code.RedeemedByUserId = userId;
            code.TransitionTo(AccessCodeStatus.Redeemed, new RedeemedState());
            code.AddDomainEvent(new AccessCodeRedeemedDomainEvent(code.Id, userId, code.TargetResourceArn, currentTime));
            return Result.Success;
        }

        public override ErrorOr<Success> Void(AccessCode code, string reason)
        {
            code.VoidedReason = reason;
            code.TransitionTo(AccessCodeStatus.Voided, new VoidedState());
            code.AddDomainEvent(new AccessCodeVoidedDomainEvent(code.Id, null, code.TargetResourceArn, reason));
            return Result.Success;
        }

        public override ErrorOr<Success> Expire(AccessCode code, DateTime? now)
        {
            var expiredAt = now ?? DateTime.UtcNow;
            code.TransitionTo(AccessCodeStatus.Expired, new ExpiredState());
            code.AddDomainEvent(new AccessCodeExpiredDomainEvent(code.Id, expiredAt));
            return Result.Success;
        }
    }

    private sealed class RedeemedState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Redeemed;

        public override ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now)
        {
            // Natural Idempotency check: if already redeemed by the same user, succeed without error
            if (code.RedeemedByUserId == userId)
            {
                return Result.Success;
            }

            return Error.Conflict("AccessCode.InvalidStatus", $"Code cannot be redeemed. Current status: {Status}");
        }

        public override ErrorOr<Success> Void(AccessCode code, string reason)
        {
            code.VoidedReason = reason;
            code.TransitionTo(AccessCodeStatus.Voided, new VoidedState());
            // Redeemed code carries the user ID so access can be revoked
            code.AddDomainEvent(new AccessCodeVoidedDomainEvent(code.Id, code.RedeemedByUserId, code.TargetResourceArn, reason));
            return Result.Success;
        }
    }

    private sealed class VoidedState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Voided;

        public override ErrorOr<Success> Void(AccessCode code, string reason)
        {
            return Error.Conflict("AccessCode.AlreadyVoided", "Code is already voided.");
        }
    }

    private sealed class ExpiredState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Expired;

        public override ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now)
        {
            return Error.Conflict("AccessCode.Expired", "This access code has expired.");
        }
    }

    #endregion
}

public class AccessCodeDistributedDomainEvent(Guid AccessCodeId, Guid? LibraryId, Guid? BatchId, DateTime DistributedAt) : DomainEvent()
{
    public Guid AccessCodeId { get; } = AccessCodeId;
    public Guid? LibraryId { get; } = LibraryId;
    public Guid? BatchId { get; } = BatchId;
    public DateTime DistributedAt { get; } = DistributedAt;
}

public class AccessCodeSoldDomainEvent(Guid AccessCodeId, Guid TenantId, Guid LibraryId, Guid SoldByUserId, decimal Price, string Currency, DateTime SoldAt) : DomainEvent()
{
    public Guid AccessCodeId { get; } = AccessCodeId;
    public Guid TenantId { get; } = TenantId;
    public Guid LibraryId { get; } = LibraryId;
    public Guid SoldByUserId { get; } = SoldByUserId;
    public decimal Price { get; } = Price;
    public string Currency { get; } = Currency;
    public DateTime SoldAt { get; } = SoldAt;
}

public class AccessCodeRedeemedDomainEvent(Guid AccessCodeId, Guid UserId, ResourceArn Resource, DateTime RedeemedAt) : DomainEvent()
{
    public Guid AccessCodeId { get; } = AccessCodeId;
    public Guid UserId { get; } = UserId;
    public ResourceArn Resource { get; } = Resource;
    public DateTime RedeemedAt { get; } = RedeemedAt;
}

public class AccessCodeVoidedDomainEvent(Guid AccessCodeId, Guid? UserId, ResourceArn Resource, string Reason) : DomainEvent()
{
    public Guid AccessCodeId { get; } = AccessCodeId;
    public Guid? UserId { get; } = UserId;
    public ResourceArn Resource { get; } = Resource;
    public string Reason { get; } = Reason;
}

public class AccessCodeExpiredDomainEvent(Guid AccessCodeId, DateTime ExpiredAt) : DomainEvent()
{
    public Guid AccessCodeId { get; } = AccessCodeId;
    public DateTime ExpiredAt { get; } = ExpiredAt;
}
