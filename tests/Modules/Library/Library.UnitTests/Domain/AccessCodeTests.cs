using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using System.Text.Json;

namespace Library.UnitTests.Domain;

public class AccessCodeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly ResourceArn TargetResource = ResourceArn.Create("az:course:tenant:course/101").Value;
    private static readonly JsonDocument Metadata = JsonDocument.Parse("{}");

    [Fact]
    public void Mint_Should_CreateCodeInMintedStatus()
    {
        // Act
        var accessCode = AccessCode.Mint("hash", TenantId, null, "strategy", TargetResource, Metadata);

        // Assert
        accessCode.Status.Should().Be(AccessCodeStatus.Minted);
        accessCode.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public void Redeem_Should_TransitionToRedeemed_WhenInValidStatus()
    {
        // Arrange
        var accessCode = AccessCode.Mint("hash", TenantId, null, "strategy", TargetResource, Metadata);
        var userId = Guid.NewGuid();

        // Act
        var result = accessCode.Redeem(userId);

        // Assert
        result.IsError.Should().BeFalse();
        accessCode.Status.Should().Be(AccessCodeStatus.Redeemed);
        accessCode.RedeemedByUserId.Should().Be(userId);
        accessCode.RedeemedAt.Should().NotBeNull();
    }

    [Fact]
    public void Redeem_Should_Fail_WhenAlreadyRedeemed()
    {
        // Arrange
        var accessCode = AccessCode.Mint("hash", TenantId, null, "strategy", TargetResource, Metadata);
        accessCode.Redeem(Guid.NewGuid());

        // Act
        var result = accessCode.Redeem(Guid.NewGuid());

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
    }

    [Fact]
    public void Void_Should_TransitionToVoided_AndAddEvent_IfPreviouslyRedeemed()
    {
        // Arrange
        var accessCode = AccessCode.Mint("hash", TenantId, null, "strategy", TargetResource, Metadata);
        var userId = Guid.NewGuid();
        accessCode.Redeem(userId);

        // Act
        var result = accessCode.Void("Stolen");

        // Assert
        result.IsError.Should().BeFalse();
        accessCode.Status.Should().Be(AccessCodeStatus.Voided);
        accessCode.DomainEvents.Should().ContainSingle(e => e is AccessCodeVoidedDomainEvent);
    }

    [Fact]
    public void Void_Should_PersistReasonOnAggregate()
    {
        // Arrange
        var accessCode = AccessCode.Mint("hash", TenantId, null, "strategy", TargetResource, Metadata);

        // Act
        var result = accessCode.Void("Damaged in transit");

        // Assert
        result.IsError.Should().BeFalse();
        accessCode.Status.Should().Be(AccessCodeStatus.Voided);
        accessCode.VoidedReason.Should().Be("Damaged in transit");
    }

    [Fact]
    public void Void_Should_CarryReasonInDomainEvent_WhenPreviouslyRedeemed()
    {
        // Arrange
        var accessCode = AccessCode.Mint("hash", TenantId, null, "strategy", TargetResource, Metadata);
        accessCode.Redeem(Guid.NewGuid());

        // Act
        accessCode.Void("Theft");

        // Assert
        var @event = accessCode.DomainEvents.Should().ContainSingle(e => e is AccessCodeVoidedDomainEvent)
            .Subject as AccessCodeVoidedDomainEvent;
        @event!.Reason.Should().Be("Theft");
    }

    [Fact]
    public void Void_Should_RejectEmptyReason_WithValidationError()
    {
        // Arrange
        var accessCode = AccessCode.Mint("hash", TenantId, null, "strategy", TargetResource, Metadata);

        // Act
        var result = accessCode.Void("   ");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.EmptyReason");
        accessCode.Status.Should().Be(AccessCodeStatus.Minted);
        accessCode.VoidedReason.Should().BeNull();
    }

    [Fact]
    public void ReVoid_Should_Conflict_AndKeepOriginalReason()
    {
        // Arrange
        var accessCode = AccessCode.Mint("hash", TenantId, null, "strategy", TargetResource, Metadata);
        accessCode.Void("Stolen");

        // Act
        var result = accessCode.Void("Different reason");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.AlreadyVoided");
        accessCode.VoidedReason.Should().Be("Stolen");
    }
}