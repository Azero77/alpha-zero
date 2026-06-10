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

        // Act
        var log = RedemptionAuditLog.Record(
            tenantId,
            libraryId,
            accessCodeId,
            userId,
            strategyId,
            resourceArn,
            redeemedAt,
            ip,
            fingerprint);

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
}
