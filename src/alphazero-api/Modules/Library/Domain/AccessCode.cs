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
        Status = _state.Status;
    }

    public static AccessCode Mint(
        string codeHash,
        Guid tenantId,
        Guid? libraryId,
        string strategyId,
        ResourceArn targetResourceArn,
        JsonDocument metadata,
        Guid? batchId = null,
        DateTime? expiresAt = null,
        Guid? actorUserId = null)
    {
        decimal? faceValue = null;
        if (metadata.RootElement.TryGetProperty("FaceValue", out var fvProp) && fvProp.TryGetDecimal(out var decFv))
        {
            faceValue = decFv;
        }

        var code = new AccessCode(
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

        code.AddDomainEvent(new AccessCodeMintedDomainEvent(
            code.Id,
            tenantId,
            libraryId,
            batchId,
            actorUserId,
            strategyId,
            targetResourceArn,
            faceValue,
            code.CreatedAt));

        return code;
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

    public ErrorOr<Success> Distribute(Guid? actorUserId = null)
    {
        return _state.Distribute(this, actorUserId);
    }

    public ErrorOr<Success> Sell(Guid libraryId, Guid soldByUserId, decimal price, string currency, DateTime? now = null)
    {
        return _state.Sell(this, libraryId, soldByUserId, price, currency, now);
    }

    public ErrorOr<Success> Redeem(Guid userId, DateTime? now = null, Guid? forensicLogId = null, bool isUnsoldRedemption = false)
    {
        return _state.Redeem(this, userId, now, forensicLogId, isUnsoldRedemption);
    }

    public ErrorOr<Success> Void(string reason, Guid? actorUserId = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("AccessCode.EmptyReason", "A void reason is required.");
        }

        return _state.Void(this, reason, actorUserId);
    }

    public ErrorOr<Success> Expire(DateTime? now = null)
    {
        return _state.Expire(this, now);
    }

    private void TransitionTo(AccessCodeState newState)
    {
        ExpectedPreviousStatus = Status;
        _state = newState;
        Status = newState.Status;
    }

    #region State Pattern Classes

    private abstract class AccessCodeState
    {
        public static readonly AccessCodeState Minted = new MintedState();
        public static readonly AccessCodeState Distributed = new DistributedState();
        public static readonly AccessCodeState Sold = new SoldState();
        public static readonly AccessCodeState Redeemed = new RedeemedState();
        public static readonly AccessCodeState Voided = new VoidedState();
        public static readonly AccessCodeState Expired = new ExpiredState();

        public abstract AccessCodeStatus Status { get; }

        public static AccessCodeState Create(AccessCodeStatus status) => status switch
        {
            AccessCodeStatus.Minted => Minted,
            AccessCodeStatus.Distributed => Distributed,
            AccessCodeStatus.Sold => Sold,
            AccessCodeStatus.Redeemed => Redeemed,
            AccessCodeStatus.Voided => Voided,
            AccessCodeStatus.Expired => Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown access code status.")
        };

        public virtual ErrorOr<Success> Distribute(AccessCode code, Guid? actorUserId = null)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code in {Status} status cannot be distributed.");

        public virtual ErrorOr<Success> Sell(AccessCode code, Guid libraryId, Guid soldByUserId, decimal price, string currency, DateTime? now)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code in {Status} status cannot be sold.");

        public virtual ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now, Guid? forensicLogId = null, bool isUnsoldRedemption = false)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code cannot be redeemed. Current status: {Status}");

        public virtual ErrorOr<Success> Void(AccessCode code, string reason, Guid? actorUserId = null)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code in {Status} status cannot be voided.");

        public virtual ErrorOr<Success> Expire(AccessCode code, DateTime? now)
            => Error.Conflict("AccessCode.InvalidStatus", $"Code in {Status} status cannot be expired.");
    }

    private sealed class MintedState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Minted;

        public override ErrorOr<Success> Distribute(AccessCode code, Guid? actorUserId = null)
        {
            code.TransitionTo(AccessCodeState.Distributed);
            code.AddDomainEvent(new AccessCodeDistributedDomainEvent(code.Id, code.TenantId, code.LibraryId, code.BatchId, DateTime.UtcNow, actorUserId));
            return Result.Success;
        }

        public override ErrorOr<Success> Void(AccessCode code, string reason, Guid? actorUserId = null)
        {
            code.VoidedReason = reason;
            code.TransitionTo(AccessCodeState.Voided);
            code.AddDomainEvent(new AccessCodeVoidedDomainEvent(code.Id, code.TenantId, code.LibraryId, actorUserId, null, code.TargetResourceArn, reason));
            return Result.Success;
        }

        public override ErrorOr<Success> Expire(AccessCode code, DateTime? now)
        {
            var expiredAt = now ?? DateTime.UtcNow;
            code.TransitionTo(AccessCodeState.Expired);
            code.AddDomainEvent(new AccessCodeExpiredDomainEvent(code.Id, code.TenantId, code.LibraryId, expiredAt));
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

            code.TransitionTo(AccessCodeState.Sold);
            code.AddDomainEvent(new AccessCodeSoldDomainEvent(code.Id, code.TenantId, libraryId, soldByUserId, price, currency, currentTime));
            return Result.Success;
        }

        public override ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now, Guid? forensicLogId = null, bool isUnsoldRedemption = false)
        {
            var currentTime = now ?? DateTime.UtcNow;
            if (code.ExpiresAt.HasValue && currentTime >= code.ExpiresAt.Value)
            {
                return Error.Conflict("AccessCode.Expired", "This access code has expired.");
            }

            code.RedeemedAt = currentTime;
            code.RedeemedByUserId = userId;
            code.TransitionTo(AccessCodeState.Redeemed);
            code.AddDomainEvent(new AccessCodeRedeemedDomainEvent(code.Id, code.TenantId, code.LibraryId, userId, code.TargetResourceArn, currentTime, forensicLogId, isUnsoldRedemption, code.Metadata));
            return Result.Success;
        }

        public override ErrorOr<Success> Void(AccessCode code, string reason, Guid? actorUserId = null)
        {
            code.VoidedReason = reason;
            code.TransitionTo(AccessCodeState.Voided);
            code.AddDomainEvent(new AccessCodeVoidedDomainEvent(code.Id, code.TenantId, code.LibraryId, actorUserId, null, code.TargetResourceArn, reason));
            return Result.Success;
        }

        public override ErrorOr<Success> Expire(AccessCode code, DateTime? now)
        {
            var expiredAt = now ?? DateTime.UtcNow;
            code.TransitionTo(AccessCodeState.Expired);
            code.AddDomainEvent(new AccessCodeExpiredDomainEvent(code.Id, code.TenantId, code.LibraryId, expiredAt));
            return Result.Success;
        }
    }

    private sealed class SoldState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Sold;

        public override ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now, Guid? forensicLogId = null, bool isUnsoldRedemption = false)
        {
            var currentTime = now ?? DateTime.UtcNow;
            if (code.ExpiresAt.HasValue && currentTime >= code.ExpiresAt.Value)
            {
                return Error.Conflict("AccessCode.Expired", "This access code has expired.");
            }

            code.RedeemedAt = currentTime;
            code.RedeemedByUserId = userId;
            code.TransitionTo(AccessCodeState.Redeemed);
            code.AddDomainEvent(new AccessCodeRedeemedDomainEvent(code.Id, code.TenantId, code.LibraryId, userId, code.TargetResourceArn, currentTime, forensicLogId, isUnsoldRedemption, code.Metadata));
            return Result.Success;
        }

        public override ErrorOr<Success> Void(AccessCode code, string reason, Guid? actorUserId = null)
        {
            code.VoidedReason = reason;
            code.TransitionTo(AccessCodeState.Voided);
            code.AddDomainEvent(new AccessCodeVoidedDomainEvent(code.Id, code.TenantId, code.LibraryId, actorUserId, null, code.TargetResourceArn, reason));
            return Result.Success;
        }

        public override ErrorOr<Success> Expire(AccessCode code, DateTime? now)
        {
            var expiredAt = now ?? DateTime.UtcNow;
            code.TransitionTo(AccessCodeState.Expired);
            code.AddDomainEvent(new AccessCodeExpiredDomainEvent(code.Id, code.TenantId, code.LibraryId, expiredAt));
            return Result.Success;
        }
    }

    private sealed class RedeemedState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Redeemed;

        public override ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now, Guid? forensicLogId = null, bool isUnsoldRedemption = false)
        {
            // Natural Idempotency check: if already redeemed by the same user, succeed without error
            if (code.RedeemedByUserId == userId)
            {
                return Result.Success;
            }

            return Error.Conflict("AccessCode.InvalidStatus", $"Code cannot be redeemed. Current status: {Status}");
        }

        public override ErrorOr<Success> Void(AccessCode code, string reason, Guid? actorUserId = null)
        {
            code.VoidedReason = reason;
            code.TransitionTo(AccessCodeState.Voided);
            // Redeemed code carries the user ID so access can be revoked
            code.AddDomainEvent(new AccessCodeVoidedDomainEvent(code.Id, code.TenantId, code.LibraryId, actorUserId, code.RedeemedByUserId, code.TargetResourceArn, reason));
            return Result.Success;
        }
    }

    private sealed class VoidedState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Voided;

        public override ErrorOr<Success> Void(AccessCode code, string reason, Guid? actorUserId = null)
        {
            return Error.Conflict("AccessCode.AlreadyVoided", "Code is already voided.");
        }
    }

    private sealed class ExpiredState : AccessCodeState
    {
        public override AccessCodeStatus Status => AccessCodeStatus.Expired;

        public override ErrorOr<Success> Redeem(AccessCode code, Guid userId, DateTime? now, Guid? forensicLogId = null, bool isUnsoldRedemption = false)
        {
            return Error.Conflict("AccessCode.Expired", "This access code has expired.");
        }
    }

    #endregion
}

public class AccessCodeMintedDomainEvent : DomainEvent, ILibraryLifecycleDomainEvent
{
    public Guid AccessCodeId { get; }
    public Guid TenantId { get; }
    public Guid? LibraryId { get; }
    public Guid? BatchId { get; }
    public Guid? ActorUserId { get; }
    public string StrategyId { get; }
    public ResourceArn TargetResourceArn { get; }
    public decimal? FaceValue { get; }
    public DateTime MintedAt { get; }

    public Guid AggregateId => AccessCodeId;
    public string Action => "Minted";
    public DateTime OccurredAt => MintedAt;
    public JsonDocument Payload { get; }

    public AccessCodeMintedDomainEvent(
        Guid accessCodeId,
        Guid tenantId,
        Guid? libraryId,
        Guid? batchId,
        Guid? actorUserId,
        string strategyId,
        ResourceArn targetResourceArn,
        decimal? faceValue,
        DateTime mintedAt)
    {
        AccessCodeId = accessCodeId;
        TenantId = tenantId;
        LibraryId = libraryId;
        BatchId = batchId;
        ActorUserId = actorUserId;
        StrategyId = strategyId;
        TargetResourceArn = targetResourceArn;
        FaceValue = faceValue;
        MintedAt = mintedAt;

        var payloadObj = new
        {
            BatchId,
            StrategyId,
            TargetResourceArn = targetResourceArn.Value,
            FaceValue
        };
        Payload = JsonDocument.Parse(JsonSerializer.Serialize(payloadObj));
    }
}

public class AccessCodeDistributedDomainEvent : DomainEvent, ILibraryLifecycleDomainEvent
{
    public Guid AccessCodeId { get; }
    public Guid TenantId { get; }
    public Guid? LibraryId { get; }
    public Guid? BatchId { get; }
    public Guid? ActorUserId { get; }
    public DateTime DistributedAt { get; }

    public Guid AggregateId => AccessCodeId;
    public string Action => "Distributed";
    public DateTime OccurredAt => DistributedAt;
    public JsonDocument Payload { get; }

    public AccessCodeDistributedDomainEvent(
        Guid accessCodeId,
        Guid tenantId,
        Guid? libraryId,
        Guid? batchId,
        DateTime distributedAt,
        Guid? actorUserId = null)
    {
        AccessCodeId = accessCodeId;
        TenantId = tenantId;
        LibraryId = libraryId;
        BatchId = batchId;
        DistributedAt = distributedAt;
        ActorUserId = actorUserId;

        var payloadObj = new
        {
            LibraryId,
            BatchId
        };
        Payload = JsonDocument.Parse(JsonSerializer.Serialize(payloadObj));
    }

    // Backwards-compatible constructor for existing tests
    public AccessCodeDistributedDomainEvent(Guid accessCodeId, Guid? libraryId, Guid? batchId, DateTime distributedAt)
        : this(accessCodeId, Guid.Empty, libraryId, batchId, distributedAt, null)
    {
    }
}

public class AccessCodeSoldDomainEvent : DomainEvent, ILibraryLifecycleDomainEvent
{
    public Guid AccessCodeId { get; }
    public Guid TenantId { get; }
    public Guid LibraryId { get; }
    public Guid SoldByUserId { get; }
    public decimal Price { get; }
    public string Currency { get; }
    public DateTime SoldAt { get; }

    public Guid AggregateId => AccessCodeId;
    Guid? ILibraryLifecycleDomainEvent.LibraryId => LibraryId;
    public Guid? ActorUserId => SoldByUserId;
    public string Action => "Sold";
    public DateTime OccurredAt => SoldAt;
    public JsonDocument Payload { get; }

    public AccessCodeSoldDomainEvent(
        Guid accessCodeId,
        Guid tenantId,
        Guid libraryId,
        Guid soldByUserId,
        decimal price,
        string currency,
        DateTime soldAt)
    {
        AccessCodeId = accessCodeId;
        TenantId = tenantId;
        LibraryId = libraryId;
        SoldByUserId = soldByUserId;
        Price = price;
        Currency = currency;
        SoldAt = soldAt;

        var payloadObj = new
        {
            Price,
            Currency,
            SoldByUserId
        };
        Payload = JsonDocument.Parse(JsonSerializer.Serialize(payloadObj));
    }
}

public class AccessCodeRedeemedDomainEvent : DomainEvent, ILibraryLifecycleDomainEvent
{
    public Guid AccessCodeId { get; }
    public Guid TenantId { get; }
    public Guid? LibraryId { get; }
    public Guid UserId { get; }
    public ResourceArn Resource { get; }
    public DateTime RedeemedAt { get; }
    public Guid? ForensicLogId { get; }
    public Guid? AuditReference => ForensicLogId;
    public bool IsUnsoldRedemption { get; }

    /// <summary>
    /// Snapshot of the redeemed code's provisioning metadata (e.g. Courses' {"plan"} reference).
    /// Carried so resource modules can express per-type activation contracts without parsing the ARN.
    /// </summary>
    public JsonDocument? Metadata { get; }

    public Guid AggregateId => AccessCodeId;
    public Guid? ActorUserId => UserId;
    public string Action => "Redeemed";
    public DateTime OccurredAt => RedeemedAt;
    public JsonDocument Payload { get; }

    public AccessCodeRedeemedDomainEvent(
        Guid accessCodeId,
        Guid tenantId,
        Guid? libraryId,
        Guid userId,
        ResourceArn resource,
        DateTime redeemedAt,
        Guid? forensicLogId = null,
        bool isUnsoldRedemption = false,
        JsonDocument? metadata = null)
    {
        AccessCodeId = accessCodeId;
        TenantId = tenantId;
        LibraryId = libraryId;
        UserId = userId;
        Resource = resource;
        RedeemedAt = redeemedAt;
        ForensicLogId = forensicLogId;
        IsUnsoldRedemption = isUnsoldRedemption;
        Metadata = metadata is null ? null : JsonDocument.Parse(metadata.RootElement.GetRawText());

        var payloadObj = new
        {
            UserId,
            Resource = resource.Value,
            ForensicLogId,
            UnsoldRedemption = isUnsoldRedemption
        };
        Payload = JsonDocument.Parse(JsonSerializer.Serialize(payloadObj));
    }

    // Backwards-compatible constructor for existing tests
    public AccessCodeRedeemedDomainEvent(Guid accessCodeId, Guid userId, ResourceArn resource, DateTime redeemedAt)
        : this(accessCodeId, Guid.Empty, null, userId, resource, redeemedAt, null, false)
    {
    }

    // Backwards-compatible constructor for existing tests (p3d producer-only handler)
    public AccessCodeRedeemedDomainEvent(
        Guid accessCodeId,
        Guid userId,
        ResourceArn resource,
        DateTime redeemedAt,
        Guid tenantId,
        Guid? auditReference)
        : this(accessCodeId, tenantId, null, userId, resource, redeemedAt, auditReference, false)
    {
    }
}

public class AccessCodeVoidedDomainEvent : DomainEvent, ILibraryLifecycleDomainEvent
{
    public Guid AccessCodeId { get; }
    public Guid TenantId { get; }
    public Guid? LibraryId { get; }
    public Guid? ActorUserId { get; }
    public Guid? UserId { get; }
    public ResourceArn Resource { get; }
    public string Reason { get; }
    public DateTime VoidedAt { get; }

    public Guid AggregateId => AccessCodeId;
    public string Action => "Voided";
    public DateTime OccurredAt => VoidedAt;
    public JsonDocument Payload { get; }

    public AccessCodeVoidedDomainEvent(
        Guid accessCodeId,
        Guid tenantId,
        Guid? libraryId,
        Guid? actorUserId,
        Guid? userId,
        ResourceArn resource,
        string reason,
        DateTime? voidedAt = null)
    {
        AccessCodeId = accessCodeId;
        TenantId = tenantId;
        LibraryId = libraryId;
        ActorUserId = actorUserId;
        UserId = userId;
        Resource = resource;
        Reason = reason;
        VoidedAt = voidedAt ?? DateTime.UtcNow;

        var payloadObj = new
        {
            Reason,
            RevokedUserId = userId,
            Resource = resource.Value
        };
        Payload = JsonDocument.Parse(JsonSerializer.Serialize(payloadObj));
    }

    // Backwards-compatible constructor for existing tests
    public AccessCodeVoidedDomainEvent(Guid accessCodeId, Guid? userId, ResourceArn resource, string reason)
        : this(accessCodeId, Guid.Empty, null, null, userId, resource, reason)
    {
    }
}

public class AccessCodeExpiredDomainEvent : DomainEvent, ILibraryLifecycleDomainEvent
{
    public Guid AccessCodeId { get; }
    public Guid TenantId { get; }
    public Guid? LibraryId { get; }
    public DateTime ExpiredAt { get; }

    public Guid AggregateId => AccessCodeId;
    public Guid? ActorUserId => null;
    public string Action => "Expired";
    public DateTime OccurredAt => ExpiredAt;
    public JsonDocument Payload { get; }

    public AccessCodeExpiredDomainEvent(
        Guid accessCodeId,
        Guid tenantId,
        Guid? libraryId,
        DateTime expiredAt)
    {
        AccessCodeId = accessCodeId;
        TenantId = tenantId;
        LibraryId = libraryId;
        ExpiredAt = expiredAt;

        var payloadObj = new
        {
            ExpiredAt
        };
        Payload = JsonDocument.Parse(JsonSerializer.Serialize(payloadObj));
    }

    // Backwards-compatible constructor for existing tests
    public AccessCodeExpiredDomainEvent(Guid accessCodeId, DateTime expiredAt)
        : this(accessCodeId, Guid.Empty, null, expiredAt)
    {
    }
}
