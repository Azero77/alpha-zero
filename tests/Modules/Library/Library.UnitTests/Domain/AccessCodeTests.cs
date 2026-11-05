using System.Text.Json;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using FluentAssertions;

namespace Library.UnitTests.Domain;

public class AccessCodeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly ResourceArn TargetResource = ResourceArn.Create("az:course:tenant:course/101").Value;
    private static readonly JsonDocument Metadata = JsonDocument.Parse("{}");

    private static AccessCode CreateMintedCode(DateTime? expiresAt = null)
        => AccessCode.Mint("hash", TenantId, Guid.NewGuid(), "strategy", TargetResource, Metadata, Guid.NewGuid(), expiresAt);

    private static AccessCode CreateDistributedCode(DateTime? expiresAt = null)
    {
        var code = CreateMintedCode(expiresAt);
        code.Distribute().IsError.Should().BeFalse();
        code.PopDomainEvents();
        return code;
    }

    private static AccessCode CreateSoldCode(DateTime? expiresAt = null)
    {
        return AccessCode.Reconstitute(
            Guid.NewGuid(),
            "hash",
            TenantId,
            Guid.NewGuid(),
            "strategy",
            TargetResource,
            Metadata,
            AccessCodeStatus.Sold,
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-1),
            null,
            null,
            null,
            expiresAt);
    }

    private static AccessCode CreateRedeemedCode(Guid userId, DateTime? expiresAt = null)
    {
        var code = CreateDistributedCode(expiresAt);
        code.Redeem(userId).IsError.Should().BeFalse();
        code.PopDomainEvents();
        return code;
    }

    private static AccessCode CreateVoidedCode(string reason = "Revoked")
    {
        var code = CreateMintedCode();
        code.Void(reason).IsError.Should().BeFalse();
        code.PopDomainEvents();
        return code;
    }

    private static AccessCode CreateExpiredCode()
    {
        var code = CreateMintedCode();
        code.Expire().IsError.Should().BeFalse();
        code.PopDomainEvents();
        return code;
    }

    #region 1. Minting & Reconstitution

    [Fact]
    public void Mint_Should_CreateCodeInMintedStatus()
    {
        var code = CreateMintedCode();

        code.Status.Should().Be(AccessCodeStatus.Minted);
        code.TenantId.Should().Be(TenantId);
        code.RedeemedAt.Should().BeNull();
        code.RedeemedByUserId.Should().BeNull();
        code.VoidedReason.Should().BeNull();
        code.ExpiresAt.Should().BeNull();
        var mintedEvent = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeMintedDomainEvent)
            .Subject as AccessCodeMintedDomainEvent;
        mintedEvent.Should().NotBeNull();
        mintedEvent!.AggregateId.Should().Be(code.Id);
        mintedEvent.Action.Should().Be("Minted");
    }

    [Fact]
    public void Mint_WithExpiresAt_Should_StoreUtcExpiry()
    {
        var expiry = DateTime.UtcNow.AddDays(30);
        var code = CreateMintedCode(expiry);

        code.ExpiresAt.Should().Be(expiry);
    }

    [Fact]
    public void Reconstitute_Should_RestoreAllFieldsAndState()
    {
        var id = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var redeemedAt = DateTime.UtcNow.AddDays(-1);
        var redeemedUserId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(10);

        var code = AccessCode.Reconstitute(
            id,
            "hash-123",
            TenantId,
            libraryId,
            "enroll-course",
            TargetResource,
            Metadata,
            AccessCodeStatus.Redeemed,
            batchId,
            createdAt,
            redeemedAt,
            redeemedUserId,
            "void-reason",
            expiresAt);

        code.Id.Should().Be(id);
        code.CodeHash.Should().Be("hash-123");
        code.TenantId.Should().Be(TenantId);
        code.LibraryId.Should().Be(libraryId);
        code.StrategyId.Should().Be("enroll-course");
        code.TargetResourceArn.Should().Be(TargetResource);
        code.Status.Should().Be(AccessCodeStatus.Redeemed);
        code.BatchId.Should().Be(batchId);
        code.CreatedAt.Should().Be(createdAt);
        code.RedeemedAt.Should().Be(redeemedAt);
        code.RedeemedByUserId.Should().Be(redeemedUserId);
        code.VoidedReason.Should().Be("void-reason");
        code.ExpiresAt.Should().Be(expiresAt);
        code.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region 2. Legal Transitions Matrix & Domain Events

    [Fact]
    public void Transition_Minted_To_Distributed_Should_Succeed_And_RaiseEvent()
    {
        var code = CreateMintedCode();

        var result = code.Distribute();

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Distributed);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Minted);
        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeDistributedDomainEvent)
            .Subject as AccessCodeDistributedDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.LibraryId.Should().Be(code.LibraryId);
        @event.BatchId.Should().Be(code.BatchId);
    }

    [Fact]
    public void Transition_Distributed_To_Redeemed_Should_Succeed_And_RaiseEvent()
    {
        var code = CreateDistributedCode();
        var userId = Guid.NewGuid();
        var auditRef = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var result = code.Redeem(userId, forensicLogId: auditRef);

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Redeemed);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Distributed);
        code.RedeemedByUserId.Should().Be(userId);
        code.RedeemedAt.Should().BeOnOrAfter(before);

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeRedeemedDomainEvent)
            .Subject as AccessCodeRedeemedDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.UserId.Should().Be(userId);
        @event.Resource.Should().Be(TargetResource);
        @event.TenantId.Should().Be(code.TenantId);
        @event.AuditReference.Should().Be(auditRef);
    }

    [Fact]
    public void Transition_Distributed_To_Sold_Should_Succeed_And_RaiseEvent()
    {
        var code = CreateDistributedCode();
        var libraryId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var result = code.Sell(libraryId, sellerId, 49.99m, "USD");

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Sold);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Distributed);

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeSoldDomainEvent)
            .Subject as AccessCodeSoldDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.TenantId.Should().Be(TenantId);
        @event.LibraryId.Should().Be(libraryId);
        @event.SoldByUserId.Should().Be(sellerId);
        @event.Price.Should().Be(49.99m);
        @event.Currency.Should().Be("USD");
        @event.SoldAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Transition_Sold_To_Redeemed_Should_Succeed_And_RaiseEvent()
    {
        var code = CreateSoldCode();
        var userId = Guid.NewGuid();

        var result = code.Redeem(userId);

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Redeemed);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Sold);
        code.RedeemedByUserId.Should().Be(userId);

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeRedeemedDomainEvent)
            .Subject as AccessCodeRedeemedDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.UserId.Should().Be(userId);
    }

    [Fact]
    public void Transition_Minted_To_Voided_Should_Succeed_And_RaiseEvent_WithNullUserId()
    {
        var code = CreateMintedCode();

        var result = code.Void("Stolen at factory");

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Voided);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Minted);
        code.VoidedReason.Should().Be("Stolen at factory");

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeVoidedDomainEvent)
            .Subject as AccessCodeVoidedDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.UserId.Should().BeNull();
        @event.Reason.Should().Be("Stolen at factory");
    }

    [Fact]
    public void Transition_Distributed_To_Voided_Should_Succeed_And_RaiseEvent_WithNullUserId()
    {
        var code = CreateDistributedCode();

        var result = code.Void("Lost in branch flood");

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Voided);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Distributed);
        code.VoidedReason.Should().Be("Lost in branch flood");

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeVoidedDomainEvent)
            .Subject as AccessCodeVoidedDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.UserId.Should().BeNull();
        @event.Reason.Should().Be("Lost in branch flood");
    }

    [Fact]
    public void Transition_Sold_To_Voided_Should_Succeed_And_RaiseEvent_WithNullUserId()
    {
        var code = CreateSoldCode();

        var result = code.Void("Chargeback before redemption");

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Voided);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Sold);
        code.VoidedReason.Should().Be("Chargeback before redemption");

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeVoidedDomainEvent)
            .Subject as AccessCodeVoidedDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.UserId.Should().BeNull();
        @event.Reason.Should().Be("Chargeback before redemption");
    }

    [Fact]
    public void Transition_Redeemed_To_Voided_Should_Succeed_And_RaiseEvent_WithUserId()
    {
        var userId = Guid.NewGuid();
        var code = CreateRedeemedCode(userId);

        var result = code.Void("Cheating student revoked");

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Voided);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Redeemed);
        code.VoidedReason.Should().Be("Cheating student revoked");

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeVoidedDomainEvent)
            .Subject as AccessCodeVoidedDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.UserId.Should().Be(userId);
        @event.Reason.Should().Be("Cheating student revoked");
    }

    [Fact]
    public void Transition_Minted_To_Expired_Should_Succeed_And_RaiseEvent()
    {
        var code = CreateMintedCode();
        var expiryTime = DateTime.UtcNow;

        var result = code.Expire(expiryTime);

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Expired);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Minted);

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeExpiredDomainEvent)
            .Subject as AccessCodeExpiredDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.ExpiredAt.Should().Be(expiryTime);
    }

    [Fact]
    public void Transition_Distributed_To_Expired_Should_Succeed_And_RaiseEvent()
    {
        var code = CreateDistributedCode();
        var expiryTime = DateTime.UtcNow;

        var result = code.Expire(expiryTime);

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Expired);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Distributed);

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeExpiredDomainEvent)
            .Subject as AccessCodeExpiredDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.ExpiredAt.Should().Be(expiryTime);
    }

    [Fact]
    public void Transition_Sold_To_Expired_Should_Succeed_And_RaiseEvent()
    {
        var code = CreateSoldCode();
        var expiryTime = DateTime.UtcNow;

        var result = code.Expire(expiryTime);

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Expired);
        code.ExpectedPreviousStatus.Should().Be(AccessCodeStatus.Sold);

        var @event = code.DomainEvents.Should().ContainSingle(e => e is AccessCodeExpiredDomainEvent)
            .Subject as AccessCodeExpiredDomainEvent;
        @event!.AccessCodeId.Should().Be(code.Id);
        @event.ExpiredAt.Should().Be(expiryTime);
    }

    #endregion

    #region 3. Illegal Transitions Matrix (Conflict + No Event)

    [Fact]
    public void Minted_Cannot_Sell()
    {
        var code = CreateMintedCode();
        code.PopDomainEvents();

        var result = code.Sell(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        code.Status.Should().Be(AccessCodeStatus.Minted);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Minted_Cannot_Redeem()
    {
        var code = CreateMintedCode();
        code.PopDomainEvents();

        var result = code.Redeem(Guid.NewGuid());

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        code.Status.Should().Be(AccessCodeStatus.Minted);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Distributed_Cannot_Distribute()
    {
        var code = CreateDistributedCode();

        var result = code.Distribute();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        code.Status.Should().Be(AccessCodeStatus.Distributed);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Sold_Cannot_Sell()
    {
        var code = CreateSoldCode();

        var result = code.Sell(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        code.Status.Should().Be(AccessCodeStatus.Sold);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Sold_Cannot_Distribute()
    {
        var code = CreateSoldCode();

        var result = code.Distribute();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        code.Status.Should().Be(AccessCodeStatus.Sold);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Redeemed_Cannot_Distribute()
    {
        var code = CreateRedeemedCode(Guid.NewGuid());

        var result = code.Distribute();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        code.Status.Should().Be(AccessCodeStatus.Redeemed);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Redeemed_Cannot_Expire()
    {
        var code = CreateRedeemedCode(Guid.NewGuid());

        var result = code.Expire();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        code.Status.Should().Be(AccessCodeStatus.Redeemed);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Redeemed_Cannot_BeRedeemed_ByDifferentUser()
    {
        var originalUser = Guid.NewGuid();
        var differentUser = Guid.NewGuid();
        var code = CreateRedeemedCode(originalUser);

        var result = code.Redeem(differentUser);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        code.Status.Should().Be(AccessCodeStatus.Redeemed);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Voided_Cannot_Distribute_Redeem_Expire_And_ReVoid_ReturnsAlreadyVoided()
    {
        var code = CreateVoidedCode("Initial Reason");

        var distRes = code.Distribute();
        distRes.IsError.Should().BeTrue();
        distRes.FirstError.Code.Should().Be("AccessCode.InvalidStatus");

        var redeemRes = code.Redeem(Guid.NewGuid());
        redeemRes.IsError.Should().BeTrue();
        redeemRes.FirstError.Code.Should().Be("AccessCode.InvalidStatus");

        var expireRes = code.Expire();
        expireRes.IsError.Should().BeTrue();
        expireRes.FirstError.Code.Should().Be("AccessCode.InvalidStatus");

        var voidRes = code.Void("Different Reason");
        voidRes.IsError.Should().BeTrue();
        voidRes.FirstError.Code.Should().Be("AccessCode.AlreadyVoided");
        code.VoidedReason.Should().Be("Initial Reason");

        code.Status.Should().Be(AccessCodeStatus.Voided);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Expired_Cannot_Distribute_Void_Expire_Or_Redeem()
    {
        var code = CreateExpiredCode();

        var distRes = code.Distribute();
        distRes.IsError.Should().BeTrue();
        distRes.FirstError.Code.Should().Be("AccessCode.InvalidStatus");

        var voidRes = code.Void("Void expired");
        voidRes.IsError.Should().BeTrue();
        voidRes.FirstError.Code.Should().Be("AccessCode.InvalidStatus");

        var expireRes = code.Expire();
        expireRes.IsError.Should().BeTrue();
        expireRes.FirstError.Code.Should().Be("AccessCode.InvalidStatus");

        var redeemRes = code.Redeem(Guid.NewGuid());
        redeemRes.IsError.Should().BeTrue();
        redeemRes.FirstError.Code.Should().Be("AccessCode.Expired");

        code.Status.Should().Be(AccessCodeStatus.Expired);
        code.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region 4. Idempotency & Validation

    [Fact]
    public void Redeemed_ReRedeem_SameUser_Should_Succeed_Idempotently_WithNoNewEvents()
    {
        var userId = Guid.NewGuid();
        var code = CreateRedeemedCode(userId);

        var retryResult = code.Redeem(userId);

        retryResult.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Redeemed);
        code.RedeemedByUserId.Should().Be(userId);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Void_Should_RejectEmptyOrWhitespaceReason_WithValidationError()
    {
        var code = CreateMintedCode();
        code.PopDomainEvents();

        var result = code.Void("   ");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.EmptyReason");
        code.Status.Should().Be(AccessCodeStatus.Minted);
        code.VoidedReason.Should().BeNull();
        code.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region 5. Expiry Boundary Cases

    [Fact]
    public void Redeem_WhenNotExpired_Should_Succeed()
    {
        var expiry = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var beforeExpiry = new DateTime(2026, 11, 5, 11, 59, 59, DateTimeKind.Utc);
        var code = CreateDistributedCode(expiresAt: expiry);

        var result = code.Redeem(Guid.NewGuid(), now: beforeExpiry);

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Redeemed);
    }

    [Fact]
    public void Redeem_WhenAtExactExpiryBoundary_Should_ReturnExpiredConflict_And_RaiseNoEvent()
    {
        var expiry = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var exactExpiry = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var code = CreateDistributedCode(expiresAt: expiry);

        var result = code.Redeem(Guid.NewGuid(), now: exactExpiry);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.Expired");
        code.Status.Should().Be(AccessCodeStatus.Distributed);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Redeem_WhenPastExpiryBoundary_Should_ReturnExpiredConflict_And_RaiseNoEvent()
    {
        var expiry = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var pastExpiry = new DateTime(2026, 11, 5, 12, 0, 1, DateTimeKind.Utc);
        var code = CreateDistributedCode(expiresAt: expiry);

        var result = code.Redeem(Guid.NewGuid(), now: pastExpiry);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.Expired");
        code.Status.Should().Be(AccessCodeStatus.Distributed);
        code.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Redeem_WhenNullExpiry_Should_Succeed_AtAnyTime()
    {
        var code = CreateDistributedCode(expiresAt: null);
        var farFuture = DateTime.UtcNow.AddYears(100);

        var result = code.Redeem(Guid.NewGuid(), now: farFuture);

        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Redeemed);
    }

    #endregion
}
