using AlphaZero.Modules.Courses.Application.Courses.Commands.Reorder;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class ReorderCommandsHandlerTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<ReorderCommandsHandlers> _logger;
    private readonly ReorderCommandsHandlers _handler;

    public ReorderCommandsHandlerTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _logger = Substitute.For<ILogger<ReorderCommandsHandlers>>();
        _handler = new ReorderCommandsHandlers(_courseRepository, _logger);
    }

    private void SetupQuery(Guid courseId, Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        _courseRepository.Query(courseId).Returns(builder);
        builder.WithSections().Returns(builder);
        builder.WithSection(Arg.Any<Guid>()).Returns(builder);
        builder.WithItems().Returns(builder);
        builder.WithResources(Arg.Any<bool>()).Returns(builder);
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
    }

    private static Course BuildCourseWithTwoSectionsAndItems()
    {
        var course = Course.Create(Guid.NewGuid(), Guid.NewGuid(), "Architecture 101", null, Guid.NewGuid()).Value;
        course.AddSection("S1");
        course.AddSection("S2");

        foreach (var section in course.Sections)
        {
            section.AddItem(Guid.NewGuid(), "L1", "Video", course.AllocateNextBitIndex());
            section.AddItem(Guid.NewGuid(), "L2", "Video", course.AllocateNextBitIndex());
        }

        return course;
    }

    [Fact]
    public async Task Handle_ReorderSections_Should_Succeed_WithFullPermutation()
    {
        // Arrange
        var course = BuildCourseWithTwoSectionsAndItems();
        var sectionA = course.Sections.First();
        var s1 = course.Sections.First();
        var s2 = course.Sections.Last();
        SetupQuery(course.Id, course);
        var command = new ReorderSectionsCommand(course.Id, [s2.Id, s1.Id]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        s2.Order.Should().Be(0, "S2 was moved to the front");
        s1.Order.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReorderSections_Should_Fail_WhenIdsContainUnknownId()
    {
        // Arrange
        var course = BuildCourseWithTwoSectionsAndItems();
        var sectionA = course.Sections.First();
        var s1 = course.Sections.First();
        var s2 = course.Sections.Last();
        SetupQuery(course.Id, course);
        var command = new ReorderSectionsCommand(course.Id, [s1.Id, Guid.NewGuid()]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        s2.Order.Should().Be(1, "reorder must be atomic — no partial application");
    }

    [Fact]
    public async Task Handle_ReorderSections_Should_Fail_WhenIdsDoNotCoverAllLiveSections()
    {
        // Arrange
        var course = BuildCourseWithTwoSectionsAndItems();
        var sectionA = course.Sections.First();
        var s1 = course.Sections.First();
        SetupQuery(course.Id, course);
        var command = new ReorderSectionsCommand(course.Id, [s1.Id]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_ReorderItems_Should_Succeed_WithFullPermutation()
    {
        // Arrange
        var course = BuildCourseWithTwoSectionsAndItems();
        var sectionA = course.Sections.First();
        var sectionItems = sectionA.Items.ToList();
        SetupQuery(course.Id, course);
        var command = new ReorderItemsCommand(course.Id, sectionA.Id, [sectionItems[1].Id, sectionItems[0].Id]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        sectionItems[0].Order.Should().Be(1);
        sectionItems[1].Order.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReorderItems_Should_Fail_WhenIdsContainUnknownId()
    {
        // Arrange
        var course = BuildCourseWithTwoSectionsAndItems();
        var sectionA = course.Sections.First();
        var sectionItems = sectionA.Items.ToList();
        SetupQuery(course.Id, course);
        var command = new ReorderItemsCommand(course.Id, sectionA.Id, [sectionItems[0].Id, Guid.NewGuid()]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        sectionItems[1].Order.Should().Be(1, "reorder must be atomic — no partial application");
    }

    [Fact]
    public async Task Handle_ReorderItems_Should_Fail_WhenIdsDoNotCoverAllLiveItems()
    {
        // Arrange
        var course = BuildCourseWithTwoSectionsAndItems();
        var sectionA = course.Sections.First();
        var sectionItems = sectionA.Items.ToList();
        SetupQuery(course.Id, course);
        var command = new ReorderItemsCommand(course.Id, sectionA.Id, [sectionItems[0].Id]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_ReorderItems_Should_Fail_WhenSectionDoesNotExist()
    {
        // Arrange
        var course = BuildCourseWithTwoSectionsAndItems();
        var sectionA = course.Sections.First();
        SetupQuery(course.Id, course);
        var command = new ReorderItemsCommand(course.Id, Guid.NewGuid(), [Guid.NewGuid()]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }
}
