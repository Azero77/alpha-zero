using AlphaZero.Modules.Library.Application.DomainEventHandlers;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using MassTransit;
using NSubstitute;

namespace Library.UnitTests.Application.DomainEventHandlers;

public class AccessCodeActivatedDomainEventHandlerTests
{
    private readonly IPublishEndpoint _bus;
    private readonly AccessCodeActivatedDomainEventHandler _handler;

    public AccessCodeActivatedDomainEventHandlerTests()
    {
        _bus = Substitute.For<IPublishEndpoint>();
        _handler = new AccessCodeActivatedDomainEventHandler(_bus);
    }

    [Fact]
    public async Task Handle_Should_PublishAccessCodeActivatedIntegrationEvent_WithCorrectContractFields()
    {
        // Arrange
        var accessCodeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var auditReference = Guid.NewGuid();
        var targetArn = ResourceArn.ForCourse(tenantId, Guid.NewGuid());
        var redeemedAt = DateTime.UtcNow;

        var domainEvent = new AccessCodeRedeemedDomainEvent(
            accessCodeId,
            userId,
            targetArn,
            redeemedAt,
            tenantId,
            auditReference);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _bus.Received(1).Publish(
            Arg.Is<AccessCodeActivatedIntegrationEvent>(e =>
                e.TenantId == tenantId &&
                e.AccessCodeId == accessCodeId &&
                e.RedeemingUserId == userId &&
                e.UserId == userId &&
                e.TargetResourceArn == targetArn &&
                e.Resource == targetArn &&
                e.RedemptionTimestamp == redeemedAt &&
                e.RedeemedAt == redeemedAt &&
                e.AuditReference == auditReference &&
                e.EventId != Guid.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_FallbackToResourceTenantId_WhenEventTenantIdIsEmpty()
    {
        // Arrange
        var accessCodeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var targetArn = ResourceArn.ForCourse(tenantId, Guid.NewGuid());
        var redeemedAt = DateTime.UtcNow;

        var domainEvent = new AccessCodeRedeemedDomainEvent(
            accessCodeId,
            userId,
            targetArn,
            redeemedAt,
            Guid.Empty,
            null);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _bus.Received(1).Publish(
            Arg.Is<AccessCodeActivatedIntegrationEvent>(e =>
                e.TenantId == tenantId &&
                e.AccessCodeId == accessCodeId &&
                e.RedeemingUserId == userId &&
                e.TargetResourceArn == targetArn &&
                e.AuditReference == null),
            Arg.Any<CancellationToken>());
    }
}
