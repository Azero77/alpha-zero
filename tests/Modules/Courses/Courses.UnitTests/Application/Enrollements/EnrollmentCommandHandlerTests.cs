using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Deactivate;
using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Enroll;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollements;

public class EnrollInCourseCommandHandlerTests
{
    private readonly IEnrollementRepository _enrollementRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<EnrollInCourseCommandHandler> _logger;
    private readonly EnrollInCourseCommandHandler _handler;

    public EnrollInCourseCommandHandlerTests()
    {
        _enrollementRepository = Substitute.For<IEnrollementRepository>();
        _courseRepository = Substitute.For<ICourseRepository>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _logger = Substitute.For<ILogger<EnrollInCourseCommandHandler>>();
        _handler = new EnrollInCourseCommandHandler(_enrollementRepository, _courseRepository, _tenantProvider, _logger);
    }

    private AlphaZero.Modules.Courses.Domain.Aggregates.Courses.Course BuildCourse()
    {
        var course = AlphaZero.Modules.Courses.Domain.Aggregates.Courses.Course.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Architecture 101", null, Guid.NewGuid()).Value;
        course.AddSection("S1");
        var section = course.Sections.First();
        section.AddItem(Guid.NewGuid(), "L1", "Video", course.AllocateNextBitIndex());
        return course;
    }

    private void SetupCourseQuery(Guid courseId, AlphaZero.Modules.Courses.Domain.Aggregates.Courses.Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        _courseRepository.Query(courseId).Returns(builder);
        builder.WithSections().Returns(builder);
        builder.WithItems(Arg.Any<bool>()).Returns(builder);
        builder.WithResources(Arg.Any<bool>()).Returns(builder);
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
        // WithSections()/WithItems() without-arg variants:
        builder.WithSections().Returns(builder);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTenantMissing()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns((Guid?)null);
        var command = new EnrollInCourseCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenCourseNotFound()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns(Guid.NewGuid());
        var courseId = Guid.NewGuid();
        SetupCourseQuery(courseId, null);
        var command = new EnrollInCourseCommand(Guid.NewGuid(), courseId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotFound");
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Should_FailWithConflict_WhenStudentAlreadyEnrolled()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        _tenantProvider.GetTenant().Returns(tenantId);
        var course = BuildCourse();
        SetupCourseQuery(course.Id, course);

        var existing = Enrollement.Create(Guid.NewGuid(), tenantId, studentId, course.Id, 1).Value;
        _enrollementRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new EnrollInCourseCommand(studentId, course.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsError.Should().BeTrue();
            result.FirstError.Type.Should().Be(ErrorType.Conflict);
            result.FirstError.Code.Should().Be("Enrollment.Exists");
        }
    }

    [Fact]
    public async Task Handle_Should_CreateEnrollment_AndReturnId_WhenCourseExists()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns(Guid.NewGuid());
        var course = BuildCourse();
        SetupCourseQuery(course.Id, course);
        _enrollementRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Enrollement?)null);

        var command = new EnrollInCourseCommand(Guid.NewGuid(), course.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsError.Should().BeFalse();
            result.Value.Should().NotBeEmpty();
        }
        _enrollementRepository.Received(1).Add(Arg.Is<Enrollement>(e =>
            e.StudentId == command.StudentId &&
            e.CourseId == course.Id &&
            e.Status == EnrollementStatus.Active));
    }
}

public class DeactivateEnrollmentCommandHandlerTests
{
    private readonly IEnrollementRepository _enrollementRepository;
    private readonly ILogger<DeactivateEnrollmentCommandHandler> _logger;
    private readonly DeactivateEnrollmentCommandHandler _handler;

    public DeactivateEnrollmentCommandHandlerTests()
    {
        _enrollementRepository = Substitute.For<IEnrollementRepository>();
        _logger = Substitute.For<ILogger<DeactivateEnrollmentCommandHandler>>();
        _handler = new DeactivateEnrollmentCommandHandler(_enrollementRepository, _logger);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenEnrollmentNotFound()
    {
        // Arrange
        _enrollementRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Enrollement?)null);
        var command = new DeactivateEnrollmentCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsError.Should().BeTrue();
            result.FirstError.Type.Should().Be(ErrorType.NotFound);
            result.FirstError.Code.Should().Be("Enrollment.NotFound");
        }
    }

    [Fact]
    public async Task Handle_Should_Deactivate_WhenEnrollmentIsActive()
    {
        // Arrange
        var enrollment = Enrollement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3).Value;
        _enrollementRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(enrollment);
        var command = new DeactivateEnrollmentCommand(enrollment.StudentId, enrollment.CourseId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsError.Should().BeFalse();
            enrollment.Status.Should().Be(EnrollementStatus.Inactive);
        }
        _enrollementRepository.Received(1).Update(enrollment);
    }

    [Fact]
    public async Task Handle_Should_TolerateAlreadyInactiveEnrollment()
    {
        // Arrange
        var enrollment = Enrollement.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3).Value;
        enrollment.Deactivate(); // already inactive
        _enrollementRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<Enrollement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(enrollment);
        var command = new DeactivateEnrollmentCommand(enrollment.StudentId, enrollment.CourseId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsError.Should().BeFalse();
            enrollment.Status.Should().Be(EnrollementStatus.Inactive);
        }
        _enrollementRepository.Received(1).Update(enrollment);
    }
}
