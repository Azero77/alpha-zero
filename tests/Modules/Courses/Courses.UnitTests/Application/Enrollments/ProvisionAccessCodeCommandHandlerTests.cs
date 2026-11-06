using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Enroll;
using AlphaZero.Modules.Courses.Application.Enrollments.Commands.ProvisionAccessCode;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Services;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Modules.Courses.Infrastructure.Repositories;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

public class ProvisionAccessCodeCommandHandlerTests : IDisposable
{
    private readonly ICourseRepository _courseRepository = Substitute.For<ICourseRepository>();
    private readonly IEnrollmentRepository _enrollmentRepository = Substitute.For<IEnrollmentRepository>();
    private readonly ICoursePlanPrincipalService _planPrincipalService = Substitute.For<ICoursePlanPrincipalService>();
    private readonly ISender _mediator = Substitute.For<ISender>();
    private readonly ILogger<ProvisionAccessCodeCommandHandler> _logger = Substitute.For<ILogger<ProvisionAccessCodeCommandHandler>>();
    private readonly ProvisionAccessCodeCommandHandler _handler;
    private readonly AppDbContext _dbContext;
    private readonly IFailedProvisioningRecordRepository _failedProvisioningRepository;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid AccessCodeId = Guid.NewGuid();

    public ProvisionAccessCodeCommandHandlerTests()
    {
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.GetTenant().Returns(TenantId);

        var dbName = $"courses-provision-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        _dbContext = new TestCoursesDbContext(options, tenantProvider);
        _failedProvisioningRepository = new FailedProvisioningRecordRepository(_dbContext);

        _handler = new ProvisionAccessCodeCommandHandler(
            _courseRepository,
            _enrollmentRepository,
            _planPrincipalService,
            _failedProvisioningRepository,
            _mediator,
            _logger);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ProvisionAccessCodeCommand CreateCommand(ResourceArn arn) =>
        new(AccessCodeId, UserId, arn, TenantId);

    private void SetupQuery(Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
        _courseRepository.Query(Arg.Any<Guid>()).Returns(builder);
    }

    [Fact]
    public async Task Handle_Should_EnsurePrincipalAndCreateEnrollment_WhenValid()
    {
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var principalId = Guid.NewGuid();
        var course = Course.Create(CourseId, TenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        var planResult = course.AddPlan("Standard", principalId, accessWindowDays: 30);
        planResult.IsError.Should().BeFalse();

        SetupQuery(course);
        _enrollmentRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Enrollment?)null);
        _planPrincipalService.EnsurePlanPrincipalAsync(CourseId, planResult.Value.Id, TenantId, Arg.Any<CancellationToken>())
            .Returns(principalId);
        var expectedEnrollmentId = Guid.NewGuid();
        _mediator.Send(Arg.Any<EnrollInCourseCommand>(), Arg.Any<CancellationToken>())
            .Returns((ErrorOr<Guid>)expectedEnrollmentId);

        var result = await _handler.Handle(CreateCommand(targetArn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _planPrincipalService.Received().EnsurePlanPrincipalAsync(CourseId, planResult.Value.Id, TenantId, Arg.Any<CancellationToken>());
        await _mediator.Received().Send(
            Arg.Is<EnrollInCourseCommand>(c => c.StudentId == UserId && c.CourseId == CourseId && c.CoursePlanPrincipalId == principalId),
            Arg.Any<CancellationToken>());
        (await _dbContext.FailedProvisioningRecords.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_BeIdempotent_WhenStudentAlreadyEnrolled()
    {
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var existingEnrollment = Enrollment.Create(Guid.NewGuid(), TenantId, UserId, CourseId).Value;
        _enrollmentRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existingEnrollment);

        var result = await _handler.Handle(CreateCommand(targetArn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _planPrincipalService.DidNotReceive().EnsurePlanPrincipalAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(Arg.Any<EnrollInCourseCommand>(), Arg.Any<CancellationToken>());
        (await _dbContext.FailedProvisioningRecords.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_BeIdempotent_WhenDuplicateDeliveryReturnsEnrollmentExistsConflict()
    {
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var principalId = Guid.NewGuid();
        var course = Course.Create(CourseId, TenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        var planResult = course.AddPlan("Standard", principalId, null);
        planResult.IsError.Should().BeFalse();

        SetupQuery(course);
        _enrollmentRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Enrollment?)null);
        _planPrincipalService.EnsurePlanPrincipalAsync(CourseId, planResult.Value.Id, TenantId, Arg.Any<CancellationToken>())
            .Returns(principalId);
        _mediator.Send(Arg.Any<EnrollInCourseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Conflict("Enrollment.Exists", "Student is already enrolled in this course."));

        var result = await _handler.Handle(CreateCommand(targetArn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        (await _dbContext.FailedProvisioningRecords.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ParkFailedProvisioning_WhenTargetResourceArnIsInvalid()
    {
        var invalidArn = ResourceArn.Create($"az:library:{TenantId}:library/{Guid.NewGuid()}").Value;

        var result = await _handler.Handle(CreateCommand(invalidArn), CancellationToken.None);

        result.IsError.Should().BeFalse();
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
    public async Task Handle_Should_ParkFailedProvisioning_WhenCourseNotFound()
    {
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        SetupQuery(null);
        _enrollmentRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Enrollment?)null);

        var result = await _handler.Handle(CreateCommand(targetArn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().ContainSingle();
        failedRecords.First().FailureReason.Should().Contain($"Course '{CourseId}' was not found");
    }

    [Fact]
    public async Task Handle_Should_ParkFailedProvisioning_WhenNoPlansExistOnCourse()
    {
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var course = Course.Create(CourseId, TenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        SetupQuery(course);
        _enrollmentRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Enrollment?)null);

        var result = await _handler.Handle(CreateCommand(targetArn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().ContainSingle();
        failedRecords.First().FailureReason.Should().Contain("No applicable plan could be resolved");
    }

    [Fact]
    public async Task Handle_Should_ParkFailedProvisioning_WhenIdentityPrincipalCreationFails()
    {
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var course = Course.Create(CourseId, TenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        var planResult = course.AddPlan("Standard", Guid.NewGuid(), null);
        planResult.IsError.Should().BeFalse();

        SetupQuery(course);
        _enrollmentRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Enrollment?)null);
        _planPrincipalService.EnsurePlanPrincipalAsync(CourseId, planResult.Value.Id, TenantId, Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Identity.PrincipalFailed", "Principal creation timed out"));

        var result = await _handler.Handle(CreateCommand(targetArn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var failedRecords = await _dbContext.FailedProvisioningRecords.ToListAsync();
        failedRecords.Should().ContainSingle();
        failedRecords.First().FailureReason.Should().Contain("Failed to ensure principal in Identity");
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
