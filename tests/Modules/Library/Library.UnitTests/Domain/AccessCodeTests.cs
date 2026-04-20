using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using System.Text.Json;

namespace Library.UnitTests.Domain;

public class AccessCodeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly ResourceArn TargetResource = ResourceArn.Create("az:courses:tenant:course/101").Value;
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
}