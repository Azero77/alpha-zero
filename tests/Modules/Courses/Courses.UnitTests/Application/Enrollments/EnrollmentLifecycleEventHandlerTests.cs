using Moq;
using AlphaZero.Modules.Courses.Application.Enrollments.EventHandlers;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Audits;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Modules.Courses.IntegrationEvents;
using FluentAssertions;
using MassTransit;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

/// <summary>
/// Lifecycle domain events stage exactly one audit row (issue #34 §3.1 — written in-process,
/// atomically with the aggregate mutation) AND map 1:1 to their integration events on the module
/// bus (issue #34 story #7 — consumers subscribe to resumption/suspension/expiry the same way they
/// already react to creation/completion). Deactivation is the deliberate exception:
/// <see cref="EnrollmentDeactivatedDomainEvent"/> produces ONLY an audit row — no bus publication.
/// </summary>
public class EnrollmentLifecycleEventHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    /// <summary>Mock that captures the first staged row; the capture is wired BEFORE the handler runs.</summary>
    private static (DomainEventHandler Handler, Mock<IPublishEndpoint> Bus, Mock<IEnrollmentAuditRepository> Audit, Func<EnrollmentAuditEntry?> Row)
        CreateHandler()
    {
        var bus = new Mock<IPublishEndpoint>();

        EnrollmentAuditEntry? captured = null;
        var audit = new Mock<IEnrollmentAuditRepository>();
        audit.Setup(r => r.AddAsync(It.IsAny<EnrollmentAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<EnrollmentAuditEntry, CancellationToken>((row, _) =>
            {
                captured ??= row;
            })
            .Returns(Task.CompletedTask);

        return (new DomainEventHandler(bus.Object, audit.Object), bus, audit, () => captured);
    }

    private static EnrollmentAuditEntry ExpectExactlyOneRow(
        Mock<IEnrollmentAuditRepository> audit, Func<EnrollmentAuditEntry?> row)
    {
        audit.Verify(
            r => r.AddAsync(It.IsAny<EnrollmentAuditEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
        return row().Should().NotBeNull().And.BeOfType<EnrollmentAuditEntry>().Subject;
    }

    [Fact]
    public async Task Handle_Should_StageSuspendedAuditRow_AndPublishIntegrationEvent_WithReason()
    {
        var (handler, bus, audit, row) = CreateHandler();
        var actorId = Guid.NewGuid();
        var domainEvent = new EnrollmentSuspendedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId, EnrollmentStatus.Active, actorId, "billing dispute");

        await handler.Handle(domainEvent, CancellationToken.None);

        var staged = ExpectExactlyOneRow(audit, row);
        staged.SubjectId.Should().Be(domainEvent.EnrollmentId);
        staged.TenantId.Should().Be(TenantId);
        staged.EventKind.Should().Be(EnrollmentAuditEventKind.Suspended);
        staged.ActorId.Should().Be(actorId);
        staged.OccurredOn.Should().Be(domainEvent.OccuredOn);
        staged.Metadata["From"].Should().Be(nameof(EnrollmentStatus.Active));
        staged.Metadata["To"].Should().Be(nameof(EnrollmentStatus.Suspended));
        staged.Metadata["cause"].Should().Be("billing dispute");

        bus.Verify(b => b.Publish(
            It.Is<EnrollmentSuspendedIntegrationEvent>(e =>
                e.EnrollmentId == domainEvent.EnrollmentId &&
                e.StudentId == domainEvent.StudentId &&
                e.CourseId == domainEvent.CourseId &&
                e.TenantId == TenantId &&
                e.Reason == "billing dispute"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_StageResumedAuditRow_AndPublishResumedIntegrationEvent()
    {
        var (handler, bus, audit, row) = CreateHandler();
        var actorId = Guid.NewGuid();
        var domainEvent = new EnrollmentResumedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId, EnrollmentStatus.Suspended, actorId);

        await handler.Handle(domainEvent, CancellationToken.None);

        var staged = ExpectExactlyOneRow(audit, row);
        staged.EventKind.Should().Be(EnrollmentAuditEventKind.Resumed);
        staged.SubjectId.Should().Be(domainEvent.EnrollmentId);
        staged.TenantId.Should().Be(TenantId);
        staged.ActorId.Should().Be(actorId);
        staged.OccurredOn.Should().Be(domainEvent.OccuredOn);
        staged.Metadata["From"].Should().Be(nameof(EnrollmentStatus.Suspended));
        staged.Metadata["To"].Should().Be(nameof(EnrollmentStatus.Active));
        staged.Metadata.Should().NotContainKey("cause");

        bus.Verify(b => b.Publish(
            It.Is<EnrollmentResumedIntegrationEvent>(e =>
                e.EnrollmentId == domainEvent.EnrollmentId &&
                e.StudentId == domainEvent.StudentId &&
                e.CourseId == domainEvent.CourseId &&
                e.TenantId == TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_StageExpiredAuditRow_AndPublishExpiredIntegrationEvent()
    {
        var (handler, bus, audit, row) = CreateHandler();
        var domainEvent = new EnrollmentExpiredDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId, EnrollmentStatus.Active);

        await handler.Handle(domainEvent, CancellationToken.None);

        var staged = ExpectExactlyOneRow(audit, row);
        staged.EventKind.Should().Be(EnrollmentAuditEventKind.Expired);
        staged.SubjectId.Should().Be(domainEvent.EnrollmentId);
        staged.TenantId.Should().Be(TenantId);
        staged.ActorId.Should().BeNull(); // expiry is automated — the audit reads "system"
        staged.OccurredOn.Should().Be(domainEvent.OccuredOn);
        staged.Metadata["From"].Should().Be(nameof(EnrollmentStatus.Active));
        staged.Metadata["To"].Should().Be(nameof(EnrollmentStatus.Expired));
        staged.Metadata.Should().NotContainKey("cause");

        bus.Verify(b => b.Publish(
            It.Is<EnrollmentExpiredIntegrationEvent>(e =>
                e.EnrollmentId == domainEvent.EnrollmentId &&
                e.StudentId == domainEvent.StudentId &&
                e.CourseId == domainEvent.CourseId &&
                e.TenantId == TenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_StageDeactivatedAuditRow_WithoutPublishingAnything()
    {
        var (handler, bus, audit, row) = CreateHandler();
        var actorId = Guid.NewGuid();
        var domainEvent = new EnrollmentDeactivatedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId, EnrollmentStatus.Active, actorId);

        await handler.Handle(domainEvent, CancellationToken.None);

        var staged = ExpectExactlyOneRow(audit, row);
        staged.EventKind.Should().Be(EnrollmentAuditEventKind.Deactivated);
        staged.SubjectId.Should().Be(domainEvent.EnrollmentId);
        staged.TenantId.Should().Be(TenantId);
        staged.ActorId.Should().Be(actorId);
        staged.OccurredOn.Should().Be(domainEvent.OccuredOn);
        staged.Metadata["From"].Should().Be(nameof(EnrollmentStatus.Active));
        staged.Metadata["To"].Should().Be(nameof(EnrollmentStatus.Inactive));

        // Deactivation keeps its integration-event-less identity (Payments revocation flow):
        // nothing at all goes on the module bus.
        bus.Invocations.Where(i => i.Method.Name.Contains("Publish", StringComparison.Ordinal))
            .Should().BeEmpty();
    }
}
