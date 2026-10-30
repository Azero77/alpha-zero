using AlphaZero.Modules.Courses.Application.Courses.Commands.Update;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using SubjectAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class UpdateCourseCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly ICourseRepository _courseRepository = Substitute.For<ICourseRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ITenantProvider _tenantProvider = Substitute.For<ITenantProvider>();
    private readonly ILogger<UpdateCourseCommandHandler> _logger = Substitute.For<ILogger<UpdateCourseCommandHandler>>();
    private readonly UpdateCourseCommandHandler _handler;

    public UpdateCourseCommandHandlerTests()
    {
        _tenantProvider.GetTenant().Returns(TenantId);
        _handler = new UpdateCourseCommandHandler(
            _courseRepository, _subjectRepository, _tenantProvider, _logger);
    }

    private void SetupCourseQuery(Guid courseId, Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        _courseRepository.Query(courseId).Returns(builder);
        builder.WithSections().Returns(builder);
        builder.WithSection(Arg.Any<Guid>()).Returns(builder);
        builder.WithItems(Arg.Any<bool>()).Returns(builder);
        builder.WithResources(Arg.Any<bool>()).Returns(builder);
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
    }

    [Fact]
    public async Task Handle_Should_CallUpdateInformation_AndMarkForUpdate()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Old Title", "Old desc", Guid.NewGuid()).Value;
        var newSubjectId = Guid.NewGuid();
        SetupCourseQuery(course.Id, course);
        _subjectRepository.Any(Arg.Any<System.Linq.Expressions.Expression<Func<SubjectAggregate, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var command = new UpdateCourseCommand(course.Id, "New Title", "New description", newSubjectId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.Title.Should().Be("New Title");
        course.Description.Should().Be("New description");
        course.SubjectId.Should().Be(newSubjectId);
        _courseRepository.Received(1).Update(course);
    }

    [Fact]
    public async Task Handle_Should_NotChangeStatus()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Old Title", null, Guid.NewGuid()).Value;
        var archiveResult = course.Archive();
        archiveResult.IsError.Should().BeFalse(); // updates must be permitted from any lifecycle state
        SetupCourseQuery(course.Id, course);
        _subjectRepository.Any(Arg.Any<System.Linq.Expressions.Expression<Func<SubjectAggregate, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var command = new UpdateCourseCommand(course.Id, "New Title", null, Guid.NewGuid());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        course.Status.Should().Be(CourseStatus.Archived);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCourseDoesNotExist()
    {
        // Arrange
        SetupCourseQuery(Guid.NewGuid(), null);
        var command = new UpdateCourseCommand(Guid.NewGuid(), "Title", null, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Course.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSubjectDoesNotExist()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Title", null, Guid.NewGuid()).Value;
        SetupCourseQuery(course.Id, course);
        _subjectRepository.Any(Arg.Any<System.Linq.Expressions.Expression<Func<SubjectAggregate, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var command = new UpdateCourseCommand(course.Id, "Title", null, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.SubjectId");
    }
}
