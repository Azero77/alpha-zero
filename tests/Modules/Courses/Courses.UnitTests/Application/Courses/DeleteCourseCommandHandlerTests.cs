using AlphaZero.Modules.Courses.Application.Courses.Commands.Delete;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class DeleteCourseCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private readonly ICourseRepository _courseRepository = Substitute.For<ICourseRepository>();
    private readonly ILogger<DeleteCourseCommandHandler> _logger = Substitute.For<ILogger<DeleteCourseCommandHandler>>();
    private readonly DeleteCourseCommandHandler _handler;

    public DeleteCourseCommandHandlerTests()
    {
        _handler = new DeleteCourseCommandHandler(_courseRepository, _logger);
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
    public async Task Handle_Should_MarkCourseSoftDeleted_AndPersist()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Doomed", null, SubjectId).Value;
        SetupCourseQuery(course.Id, course);

        // Act
        var result = await _handler.Handle(new DeleteCourseCommand(course.Id), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.IsDeleted.Should().BeTrue();
        course.OnDeleted.Should().NotBeNull();
        _courseRepository.Received(1).Update(course);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCourseDoesNotExist()
    {
        // Arrange
        SetupCourseQuery(Guid.NewGuid(), null);

        // Act
        var result = await _handler.Handle(new DeleteCourseCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotFound");
    }
}
