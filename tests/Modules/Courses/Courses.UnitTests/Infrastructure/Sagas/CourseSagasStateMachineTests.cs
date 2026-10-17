using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.Infrastructure.Sagas.CourseRedemption;
using AlphaZero.Modules.Courses.Infrastructure.Sagas.CourseRevocation;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Courses.UnitTests.Infrastructure.Sagas;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Sagas;

/// <summary>
/// State-machine coverage for the money-path sagas (course redemption / revocation).
/// Drives the real state machines through the MassTransit test harness; the module
/// consumers that fulfill the saga's outgoing sends are replaced by test forwarders
/// that record received outgoing commands for assertion; the test methods publish
/// the continuation events themselves so intermediate states are observable.
/// </summary>
public class CourseRedemptionSagaTests : SagaHarnessTestBase<CourseRedemptionSaga, CourseRedemptionState>
{
    protected override void ConfigureSaga(IBusRegistrationConfigurator configurator)
    {
        configurator.AddSagaStateMachine<CourseRedemptionSaga, CourseRedemptionState>().InMemoryRepository();
        configurator.AddConsumer<EnrollForwarder>();
        configurator.AddConsumer<AssignRoleForwarder>();
        configurator.AddConsumer<RevokeForwarder>();
    }

    [Fact]
    public async Task Saga_Should_ReachEnrolling_AndSendEnrollCommand_WhenAccessUnlocked()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var command = CourseAccessUnlocked(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), tenantId, courseId, "Standard");

        // Act
        await Harness.Bus.Publish(command);

        // Assert: state persisted and outgoing enroll command sent
        (await MachineHarness().Exists(
                x => x.UserId == command.UserId && x.AccessCodeId == command.AccessCodeId && x.Plan == "Standard",
                Machine.Enrolling, TestStateTimeout))
            .Should().Contain(command.Id, "the saga must reach Enrolling with the unlock payload persisted");

        await WaitUntilAsync(() => Recorder.Received<EnrollStudentFromSagaCommand>().Any(c => c.CorrelationId == command.Id));
        Recorder.Received<EnrollStudentFromSagaCommand>().Should().ContainSingle(c =>
            c.CorrelationId == command.Id && c.UserId == command.UserId && c.CourseId == courseId);
    }

    [Fact]
    public async Task Saga_Should_Finalize_WhenEnrollmentAndRoleAssignmentSucceed()
    {
        // Arrange
        var command = CourseAccessUnlocked(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Standard");

        var machineHarness = MachineHarness();

        // Act: unlock -> enrollment confirmed -> role assigned (events published manually
        // to keep every intermediate state deterministically observable)
        var principalId = Guid.NewGuid();
        await Harness.Bus.Publish(command);
        await MachineHarness().Exists(command.Id, Machine.Enrolling, TestStateTimeout);

        await Harness.Bus.Publish(new StudentEnrolledFromSagaEvent(command.Id, principalId));
        await MachineHarness().Exists(command.Id, Machine.Authorizing, TestStateTimeout);

        await Harness.Bus.Publish(new StudentRoleAssignedFromSagaEvent(command.Id));

        // Assert: the machine ran Enrolling -> Authorizing -> Final, recording both timestamps
        // (note: CourseRedemptionSaga.Finalize() only transitions to Final - the instance stays
        //  in the repository, which is flagged as a follow-up observation on the issue)
        (await MachineHarness().Exists(x => x.EnrolledAt != null && x.AuthorizedAt != null, Machine.Final, TestStateTimeout))
            .Should().Contain(command.Id, "the finalized saga must carry the enroll and authorize timestamps");

        await WaitUntilAsync(() => Recorder.Received<AssignStudentRoleFromSagaCommand>().Any(c => c.CorrelationId == command.Id));
        Recorder.Received<EnrollStudentFromSagaCommand>().Should().ContainSingle();
        Recorder.Received<AssignStudentRoleFromSagaCommand>().Should().ContainSingle(c =>
            c.CorrelationId == command.Id && c.PrincipalId == principalId);
    }

    [Fact]
    public async Task Saga_Should_Fail_AndRecordReason_WhenEnrollmentFails()
    {
        // Arrange
        var command = CourseAccessUnlocked(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Standard");
        await Harness.Bus.Publish(command);
        await MachineHarness().Exists(command.Id, Machine.Enrolling, TestStateTimeout);

        // Act: the enroll consumer reports failure
        await Harness.Bus.Publish(new StudentEnrollmentFailedEvent(command.Id, "Course not found."));

        // Assert
        (await MachineHarness().Exists(
                x => x.FailureReason == "Course not found.", Machine.Failed, TestStateTimeout))
            .Should().Contain(command.Id, "the Failed state must carry the enroll consumer's reason");

        Recorder.Received<AssignStudentRoleFromSagaCommand>().Should().BeEmpty(
            "role assignment is never attempted when enrollment failed");
    }

    [Fact]
    public async Task Saga_Should_Compensate_WhenRoleAssignmentFails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var command = CourseAccessUnlocked(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), tenantId, courseId, "Standard");
        await Harness.Bus.Publish(command);
        await MachineHarness().Exists(command.Id, Machine.Enrolling, TestStateTimeout);

        await Harness.Bus.Publish(new StudentEnrolledFromSagaEvent(command.Id, Guid.NewGuid()));
        await MachineHarness().Exists(command.Id, Machine.Authorizing, TestStateTimeout);

        // Act: the role-assignment consumer reports failure
        await Harness.Bus.Publish(new StudentRoleAssignmentFailedEvent(command.Id, "Role assignment rejected."));

        // Assert: failed state + the compensating revocation command was sent
        (await MachineHarness().Exists(
                x => x.FailureReason == "Role assignment rejected.", Machine.Failed, TestStateTimeout))
            .Should().Contain(command.Id, "the Failed state must carry the role-assignment failure reason");

        await WaitUntilAsync(() => Recorder.Received<RevokeStudentEnrollmentFromSagaCommand>().Any(c => c.CorrelationId == command.Id));
        var compensation = Recorder.Received<RevokeStudentEnrollmentFromSagaCommand>();
        compensation.Should().ContainSingle().Which.Should().Match<RevokeStudentEnrollmentFromSagaCommand>(c =>
            c.CorrelationId == command.Id && c.UserId == command.UserId && c.CourseId == courseId,
            "compensation revokes the enrollment of the unlocking user on the unlocked course");
    }

    private static CourseAccessUnlockedIntegrationEvent CourseAccessUnlocked(
        Guid correlationId, Guid accessCodeId, Guid userId, Guid tenantId, Guid courseId, string plan)
        => new(correlationId, accessCodeId, DateTime.UtcNow, userId,
            ResourceArn.ForCourse(tenantId, courseId), plan);
}

public class CourseRevocationSagaTests : SagaHarnessTestBase<CourseRevocationSaga, CourseRevocationState>
{
    protected override void ConfigureSaga(IBusRegistrationConfigurator configurator)
    {
        configurator.AddSagaStateMachine<CourseRevocationSaga, CourseRevocationState>().InMemoryRepository();
        configurator.AddConsumer<RevokeForwarder>();
        configurator.AddConsumer<RemoveRoleForwarder>();
    }

    [Fact]
    public async Task Saga_Should_ReachRevokingEnrollment_AndSendRevokeCommand_WhenAccessCodeRevoked()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = RevokedEvent(Guid.NewGuid(), Guid.NewGuid(), userId, tenantId, courseId);

        // Act
        await Harness.Bus.Publish(command);

        // Assert
        (await MachineHarness().Exists(
                x => x.UserId == userId && x.AccessCodeId == command.AccessCodeId,
                Machine.RevokingEnrollment, TestStateTimeout))
            .Should().Contain(command.EventId, "the saga must reach RevokingEnrollment with the revoke payload persisted");

        await WaitUntilAsync(() => Recorder.Received<RevokeStudentEnrollmentFromSagaCommand>().Any(c => c.CorrelationId == command.EventId));
        var received = Recorder.Received<RevokeStudentEnrollmentFromSagaCommand>();
        received.Should().ContainSingle().Which.Should().Match<RevokeStudentEnrollmentFromSagaCommand>(c =>
            c.CorrelationId == command.EventId && c.UserId == userId && c.CourseId == courseId);
    }

    [Fact]
    public async Task Saga_Should_Finalize_WhenRevocationAndRoleRemovalSucceed()
    {
        // Arrange
        var command = RevokedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act: revoke -> enrollment revoked -> role removed (events published manually
        // so every intermediate state is deterministically observable)
        await Harness.Bus.Publish(command);
        await MachineHarness().Exists(command.EventId, Machine.RevokingEnrollment, TestStateTimeout);

        await Harness.Bus.Publish(new StudentEnrollmentRevokedFromSagaEvent(command.EventId));
        await MachineHarness().Exists(command.EventId, Machine.RemovingAuthorization, TestStateTimeout);

        await Harness.Bus.Publish(new StudentRoleRemovedFromSagaEvent(command.EventId));

        // Assert: the machine reached Final with all three timestamps recorded
        // (note: CourseRevocationSaga.Finalize() only transitions to Final - the instance stays
        //  in the repository, which is flagged as a follow-up observation on the issue)
        (await MachineHarness().Exists(x => x.UnauthorizedAt != null, Machine.Final, TestStateTimeout))
            .Should().Contain(command.EventId, "the finalized saga must carry the authorization-removal timestamp");

        await WaitUntilAsync(() => Recorder.Received<RemoveStudentRoleFromSagaCommand>().Any(c => c.CorrelationId == command.EventId));
        Recorder.Received<RevokeStudentEnrollmentFromSagaCommand>().Should().ContainSingle();
        Recorder.Received<RemoveStudentRoleFromSagaCommand>().Should().ContainSingle(c =>
            c.CorrelationId == command.EventId);
    }

    [Fact]
    public async Task Saga_Should_Fail_WhenEnrollmentRevocationFails()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var command = RevokedEvent(eventId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await Harness.Bus.Publish(command);
        await MachineHarness().Exists(eventId, Machine.RevokingEnrollment, TestStateTimeout);

        // Act: the revoke consumer reports failure
        await Harness.Bus.Publish(new StudentEnrollmentRevocationFailedEvent(eventId, "Enrollment revocation command failed."));

        // Assert
        (await MachineHarness().Exists(
                x => x.FailureReason == "Enrollment revocation command failed.", Machine.Failed, TestStateTimeout))
            .Should().Contain(eventId, "the Failed state must carry the revocation failure reason");

        Recorder.Received<RemoveStudentRoleFromSagaCommand>().Should().BeEmpty();
    }

    [Fact]
    public async Task Saga_Should_Fail_WhenRoleRemovalFails()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var command = RevokedEvent(eventId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await Harness.Bus.Publish(command);
        await MachineHarness().Exists(eventId, Machine.RevokingEnrollment, TestStateTimeout);

        await Harness.Bus.Publish(new StudentEnrollmentRevokedFromSagaEvent(eventId));
        await MachineHarness().Exists(eventId, Machine.RemovingAuthorization, TestStateTimeout);

        // Act
        await Harness.Bus.Publish(new StudentRoleRemovalFailedEvent(eventId, "Role removal timed out."));

        // Assert
        (await MachineHarness().Exists(
                x => x.FailureReason == "Role removal timed out.", Machine.Failed, TestStateTimeout))
            .Should().Contain(eventId, "the Failed state must carry the role-removal failure reason");
    }

    private static AccessCodeRevokedIntegrationEvent RevokedEvent(
        Guid eventId, Guid accessCodeId, Guid userId, Guid tenantId, Guid courseId)
        => new(eventId, accessCodeId, userId, ResourceArn.ForCourse(tenantId, courseId), DateTime.UtcNow);
}
/// <summary>
/// EF store persistence round-trip for the saga state mapping: the same shape the
/// production Postgres store persists (same AppDbContext model), round-tripped here
/// through the EF in-memory provider because the MT EF saga repository is Postgres-only.
/// </summary>
public class CourseSagaStatePersistenceTests
{
    private static TenantProviderStub TenantStub { get; } = new();

    private static DbContextOptions<AppDbContext> BuildOptions(string dbName) =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;

    /// <summary>
    /// The EF in-memory provider cannot map the Enrollement.Progress bitmask (BitArray is
    /// only mapped on Postgres as varbit), so it is excluded; saga state rows never touch it.
    /// </summary>
    private sealed class SagaStateTestDbContext : AppDbContext
    {
        public SagaStateTestDbContext(DbContextOptions<AppDbContext> options, Shared.Infrastructure.Tenats.ITenantProvider tenantProvider)
            : base(options, tenantProvider)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements.Enrollement>().Ignore(e => e.Progress);
        }
    }

    [Fact]
    public async Task CourseRevocationState_Should_RoundTripThroughAppDbContext()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var options = BuildOptions($"revocation-{correlationId}");
        var instance = new CourseRevocationState
        {
            CorrelationId = correlationId,
            CurrentState = nameof(CourseRevocationSaga.RemovingAuthorization),
            UserId = Guid.NewGuid(),
            AccessCodeId = Guid.NewGuid(),
            ResourceArn = ResourceArn.ForCourse(Guid.NewGuid(), Guid.NewGuid()).ToString(),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow,
        };

        // Act
        await using (var writeContext = new SagaStateTestDbContext(options, TenantStub))
        {
            writeContext.CourseRevocationStates.Add(instance);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        await using (var readContext = new SagaStateTestDbContext(options, TenantStub))
        {
            var persisted = await readContext.CourseRevocationStates.FindAsync(correlationId);
            persisted.Should().NotBeNull();
            persisted!.CurrentState.Should().Be(nameof(CourseRevocationSaga.RemovingAuthorization));
            persisted.UserId.Should().Be(instance.UserId);
            persisted.ResourceArn.Should().Be(instance.ResourceArn);
            persisted.RevokedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task CourseRedemptionState_Should_RoundTripThroughAppDbContext()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var options = BuildOptions($"redemption-{correlationId}");
        var instance = new CourseRedemptionState
        {
            CorrelationId = correlationId,
            CurrentState = nameof(CourseRedemptionSaga.Authorizing),
            UserId = Guid.NewGuid(),
            AccessCodeId = Guid.NewGuid(),
            CourseArn = ResourceArn.ForCourse(Guid.NewGuid(), Guid.NewGuid()).ToString(),
            Plan = "Standard",
            CreatedAt = DateTime.UtcNow,
            EnrolledAt = DateTime.UtcNow,
        };

        // Act
        await using (var writeContext = new SagaStateTestDbContext(options, TenantStub))
        {
            writeContext.CourseRedemptionStates.Add(instance);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        await using (var readContext = new SagaStateTestDbContext(options, TenantStub))
        {
            var persisted = await readContext.CourseRedemptionStates.FindAsync(correlationId);
            persisted.Should().NotBeNull();
            persisted!.CurrentState.Should().Be(nameof(CourseRedemptionSaga.Authorizing));
            persisted.Plan.Should().Be("Standard");
            persisted.UserId.Should().Be(instance.UserId);
            persisted.EnrolledAt.Should().NotBeNull();
        }
    }
}

public class TenantProviderStub : AlphaZero.Shared.Infrastructure.Tenats.ITenantProvider
{
    public Guid? GetTenant() => Guid.NewGuid();
}
