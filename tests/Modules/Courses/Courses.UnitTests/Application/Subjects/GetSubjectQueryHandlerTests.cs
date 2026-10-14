using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Subjects;

public class GetSubjectQueryHandlerTests
{
    private readonly ISubjectQueryService _subjectQueryService;
    private readonly GetSubjectQueryHandler _handler;

    public GetSubjectQueryHandlerTests()
    {
        _subjectQueryService = Substitute.For<ISubjectQueryService>();
        _handler = new GetSubjectQueryHandler(_subjectQueryService);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSubjectDoesNotExist()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        _subjectQueryService.GetSubjectByIdAsync(subjectId, Arg.Any<CancellationToken>())
            .Returns((SubjectDto?)null);

        var query = new GetSubjectQuery(subjectId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Subject.NotFound");
        result.FirstError.Description.Should().Be($"Subject with ID {subjectId} was not found.");
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Should_ReturnSubject_WhenFound()
    {
        // Arrange
        var subjectId = Guid.NewGuid();
        var dto = new SubjectDto(subjectId, "Mathematics", "High school mathematics");

        _subjectQueryService.GetSubjectByIdAsync(subjectId, Arg.Any<CancellationToken>())
            .Returns(dto);

        var query = new GetSubjectQuery(subjectId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEquivalentTo(dto);
        await _subjectQueryService.Received(1).GetSubjectByIdAsync(subjectId, Arg.Any<CancellationToken>());
    }
}
