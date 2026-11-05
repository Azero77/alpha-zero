using AlphaZero.Modules.Courses.Application;
using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Enroll;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Services;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Infrastructure.Consumers;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Consumers;

public class AccessCodeActivatedConsumerTests : IDisposable
{
    private readonly Mock<ICoursesModule> _coursesModuleMock = new();
    private readonly Mock<ICourseRepository> _courseRepositoryMock = new();
    private readonly Mock<IEnrollmentRepository> _enrollmentRepositoryMock = new();
    private readonly Mock<ICoursePlanPrincipalService> _planPrincipalServiceMock = new();
    private readonly Mock<ITenantProvider> _tenantProviderMock = new();
    private readonly IAmbientTenantContext _ambientTenant = new AmbientTenantContext();
    private readonly Mock<ILogger<AccessCodeActivatedConsumer>> _loggerMock = new();
    private readonly AppDbContext _dbContext;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid AccessCodeId = Guid.NewGuid();

    public AccessCodeActivatedConsumerTests()
    {
        var dbName = $"courses-consumer-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _tenantProviderMock.Setup(t => t.GetTenant()).Returns(TenantId);

        _dbContext = new TestCoursesDbContext(options, _tenantProviderMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private AccessCodeActivatedConsumer CreateConsumer()
    {
        return new AccessCodeActivatedConsumer(
            _coursesModuleMock.Object,
            _courseRepositoryMock.Object,
            _enrollmentRepositoryMock.Object,
            _planPrincipalServiceMock.Object,
            _dbContext,
            _tenantProviderMock.Object,
            _ambientTenant,
            _loggerMock.Object);
    }

    private static ConsumeContext<AccessCodeActivatedIntegrationEvent> CreateContext(AccessCodeActivatedIntegrationEvent message)
    {
        var mock = new Mock<ConsumeContext<AccessCodeActivatedIntegrationEvent>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    private void SetupQuery(Course? course)
    {
        var builderMock = new Mock<ICourseQueryBuilder>();
        builderMock.Setup(b => b.WithPlans(It.IsAny<bool>())).Returns(builderMock.Object);
        builderMock.Setup(b => b.ExecuteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(course);
        _courseRepositoryMock.Setup(x => x.Query(It.IsAny<Guid>())).Returns(builderMock.Object);
    }

    [Fact]
    public async Task Consume_Should_EnsurePrincipalAndCreateEnrollment_WhenValidActivationEvent()
    {
        // Arrange
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var activatedEvent = new AccessCodeActivatedIntegrationEvent(
            Guid.NewGuid(),
            TenantId,
            AccessCodeId,
            UserId,
            targetArn,
            DateTime.UtcNow,
            Guid.NewGuid());

        var principalId = Guid.NewGuid();
        var course = Course.Create(CourseId, TenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        var planResult = course.AddPlan("Standard", principalId, accessWindowDays: 30);
        planResult.IsError.Should().BeFalse();

        SetupQuery(course);

        // No existing enrollment
        _enrollmentRepositoryMock.Setup(r => r.GetFirst(It.IsAny<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enrollment?)null);

        // Identity principal service returns principalId
        _planPrincipalServiceMock.Setup(s => s.EnsurePlanPrincipalAsync(CourseId, planResult.Value.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(principalId);

        // Enroll command succeeds
        var expectedEnrollmentId = Guid.NewGuid();
        _coursesModuleMock.Setup(m => m.Send<ErrorOr<Guid>>(It.IsAny<EnrollInCourseCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEnrollmentId);

        var consumer = CreateConsumer();

        // Act
        await consumer.Consume(CreateContext(activatedEvent));

        // Assert: Principal was ensured in Identity
        _planPrincipalServiceMock.Verify(
            s => s.EnsurePlanPrincipalAsync(CourseId, planResult.Value.Id, TenantId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: Enrollment command was sent to Courses module
        _coursesModuleMock.Verify(
            m => m.Send<ErrorOr<Guid>>(
                It.Is<EnrollInCourseCommand>(c =>
                    c.StudentId == UserId &&
                    c.CourseId == CourseId &&
                    c.CoursePlanPrincipalId == principalId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: Zero failed provisioning records
        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_Should_BeIdempotent_WhenStudentAlreadyEnrolled()
    {
        // Arrange
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var activatedEvent = new AccessCodeActivatedIntegrationEvent(
            Guid.NewGuid(),
            TenantId,
            AccessCodeId,
            UserId,
            targetArn,
            DateTime.UtcNow,
            Guid.NewGuid());

        var existingEnrollment = Enrollment.Create(Guid.NewGuid(), TenantId, UserId, CourseId).Value;
        _enrollmentRepositoryMock.Setup(r => r.GetFirst(It.IsAny<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEnrollment);

        var consumer = CreateConsumer();

        // Act
        await consumer.Consume(CreateContext(activatedEvent));

        // Assert: Neither principal nor enrollment was recreated
        _planPrincipalServiceMock.Verify(
            s => s.EnsurePlanPrincipalAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _coursesModuleMock.Verify(
            m => m.Send<ErrorOr<Guid>>(It.IsAny<EnrollInCourseCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_Should_BeIdempotent_WhenDuplicateDeliveryReturnsEnrollmentExistsConflict()
    {
        // Arrange
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var activatedEvent = new AccessCodeActivatedIntegrationEvent(
            Guid.NewGuid(),
            TenantId,
            AccessCodeId,
            UserId,
            targetArn,
            DateTime.UtcNow,
            Guid.NewGuid());

        var principalId = Guid.NewGuid();
        var course = Course.Create(CourseId, TenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        var planResult = course.AddPlan("Standard", principalId, null);
        planResult.IsError.Should().BeFalse();

        SetupQuery(course);

        _enrollmentRepositoryMock.Setup(r => r.GetFirst(It.IsAny<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enrollment?)null);

        _planPrincipalServiceMock.Setup(s => s.EnsurePlanPrincipalAsync(CourseId, planResult.Value.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(principalId);

        // Module returns Conflict Enrollment.Exists (duplicate delivery storm)
        _coursesModuleMock.Setup(m => m.Send<ErrorOr<Guid>>(It.IsAny<EnrollInCourseCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Conflict("Enrollment.Exists", "Student is already enrolled in this course."));

        var consumer = CreateConsumer();

        // Act
        await consumer.Consume(CreateContext(activatedEvent));

        // Assert: Did not fail or park as failure, safely handled duplicate
        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_Should_ParkFailedProvisioning_WhenTargetResourceArnIsInvalid()
    {
        // Arrange: ARN points to an invalid resource path (not a course)
        var invalidArn = ResourceArn.Create($"az:library:{TenantId}:library/{Guid.NewGuid()}").Value;
        var activatedEvent = new AccessCodeActivatedIntegrationEvent(
            Guid.NewGuid(),
            TenantId,
            AccessCodeId,
            UserId,
            invalidArn,
            DateTime.UtcNow,
            Guid.NewGuid());

        var consumer = CreateConsumer();

        // Act
        await consumer.Consume(CreateContext(activatedEvent));

        // Assert: Parked as failed provisioning record in database
        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().ContainSingle();
        var record = failedRecords.First();
        record.TenantId.Should().Be(TenantId);
        record.AccessCodeId.Should().Be(AccessCodeId);
        record.UserId.Should().Be(UserId);
        record.Status.Should().Be(ProvisioningStatus.Failed);
        record.FailureReason.Should().Contain("Invalid or malformed target resource ARN");
    }

    [Fact]
    public async Task Consume_Should_ParkFailedProvisioning_WhenCourseNotFound()
    {
        // Arrange
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var activatedEvent = new AccessCodeActivatedIntegrationEvent(
            Guid.NewGuid(),
            TenantId,
            AccessCodeId,
            UserId,
            targetArn,
            DateTime.UtcNow,
            Guid.NewGuid());

        SetupQuery(null);

        _enrollmentRepositoryMock.Setup(r => r.GetFirst(It.IsAny<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enrollment?)null);

        var consumer = CreateConsumer();

        // Act
        await consumer.Consume(CreateContext(activatedEvent));

        // Assert: Parked as failed provisioning record
        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().ContainSingle();
        var record = failedRecords.First();
        record.Status.Should().Be(ProvisioningStatus.Failed);
        record.FailureReason.Should().Contain($"Course '{CourseId}' was not found");
    }

    [Fact]
    public async Task Consume_Should_ParkFailedProvisioning_WhenNoPlansExistOnCourse()
    {
        // Arrange: Course exists but has zero plans
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var activatedEvent = new AccessCodeActivatedIntegrationEvent(
            Guid.NewGuid(),
            TenantId,
            AccessCodeId,
            UserId,
            targetArn,
            DateTime.UtcNow,
            Guid.NewGuid());

        var course = Course.Create(CourseId, TenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;

        SetupQuery(course);

        _enrollmentRepositoryMock.Setup(r => r.GetFirst(It.IsAny<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enrollment?)null);

        var consumer = CreateConsumer();

        // Act
        await consumer.Consume(CreateContext(activatedEvent));

        // Assert: Parked as failed provisioning record
        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().ContainSingle();
        var record = failedRecords.First();
        record.Status.Should().Be(ProvisioningStatus.Failed);
        record.FailureReason.Should().Contain("No applicable plan could be resolved");
    }

    [Fact]
    public async Task Consume_Should_ParkFailedProvisioning_WhenIdentityPrincipalCreationFails()
    {
        // Arrange
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var activatedEvent = new AccessCodeActivatedIntegrationEvent(
            Guid.NewGuid(),
            TenantId,
            AccessCodeId,
            UserId,
            targetArn,
            DateTime.UtcNow,
            Guid.NewGuid());

        var course = Course.Create(CourseId, TenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        var planResult = course.AddPlan("Standard", Guid.NewGuid(), null);
        planResult.IsError.Should().BeFalse();

        SetupQuery(course);

        _enrollmentRepositoryMock.Setup(r => r.GetFirst(It.IsAny<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enrollment?)null);

        _planPrincipalServiceMock.Setup(s => s.EnsurePlanPrincipalAsync(CourseId, planResult.Value.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Failure("Identity.PrincipalFailed", "Principal creation timed out"));

        var consumer = CreateConsumer();

        // Act
        await consumer.Consume(CreateContext(activatedEvent));

        // Assert: Parked as failed provisioning record
        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().ContainSingle();
        var record = failedRecords.First();
        record.Status.Should().Be(ProvisioningStatus.Failed);
        record.FailureReason.Should().Contain("Failed to ensure principal in Identity");
    }

    private sealed class TestCoursesDbContext : AppDbContext
    {
        public TestCoursesDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
            : base(options, tenantProvider)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Enrollment>().Ignore(e => e.Progress);
            modelBuilder.Entity<Course>().Ignore(c => c.ActiveItemsBitmask);
        }
    }
}
