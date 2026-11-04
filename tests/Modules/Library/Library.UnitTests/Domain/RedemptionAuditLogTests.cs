using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using FluentAssertions;

namespace AlphaZero.Modules.Library.UnitTests.Domain;

public class RedemptionAuditLogTests
{
    [Fact]
    public void Record_Should_Set_All_Properties_Correctly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var accessCodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var strategyId = "test-strategy";
        var resourceArn = ResourceArn.Create("course", tenantId.ToString(), "math-101").Value;
        var redeemedAt = DateTime.UtcNow;
        var ip = "127.0.0.1";
        var fingerprint = "fingerprint";

        var context = new RedemptionContext(
            tenantId,
            libraryId,
            userId,
            strategyId,
            resourceArn,
            redeemedAt,
            ip,
            fingerprint);

        // Act
        var log = RedemptionAuditLog.Record(context, accessCodeId);

        // Assert
        log.Id.Should().NotBeEmpty();
        log.TenantId.Should().Be(tenantId);
        log.LibraryId.Should().Be(libraryId);
        log.AccessCodeId.Should().Be(accessCodeId);
        log.RedeemedByUserId.Should().Be(userId);
        log.StrategyId.Should().Be(strategyId);
        log.TargetResourceArn.Should().Be(resourceArn);
        log.RedeemedAt.Should().Be(redeemedAt);
        log.IpAddress.Should().Be(ip);
        log.DeviceFingerprint.Should().Be(fingerprint);
    }

    [Fact]
    public void RedemptionContext_Should_ConstructFromHandlerInputsCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var libraryId = (Guid?)Guid.NewGuid();
        var userId = Guid.NewGuid();
        var strategyId = "course-enrollment";
        var resourceArn = ResourceArn.Create("course", tenantId.ToString(), "cs-50").Value;
        var redeemedAt = DateTime.UtcNow;
        var ip = "192.168.1.100";
        var fingerprint = "fp-abc-123";

        // Act
        var context = new RedemptionContext(
            TenantId: tenantId,
            LibraryId: libraryId,
            RedeemedByUserId: userId,
            StrategyId: strategyId,
            TargetResourceArn: resourceArn,
            RedeemedAt: redeemedAt,
            IpAddress: ip,
            DeviceFingerprint: fingerprint);

        // Assert
        context.TenantId.Should().Be(tenantId);
        context.LibraryId.Should().Be(libraryId);
        context.RedeemedByUserId.Should().Be(userId);
        context.UserId.Should().Be(userId);
        context.StrategyId.Should().Be(strategyId);
        context.TargetResourceArn.Should().Be(resourceArn);
        context.RedeemedAt.Should().Be(redeemedAt);
        context.IpAddress.Should().Be(ip);
        context.DeviceFingerprint.Should().Be(fingerprint);
    }
}
