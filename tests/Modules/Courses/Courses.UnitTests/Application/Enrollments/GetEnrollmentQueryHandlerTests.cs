using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Modules.Courses.Application.Queries;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

public class GetEnrollmentQueryHandlerTests
{
    private readonly IEnrollmentQueryService _enrollmentQueryService;
    private readonly GetEnrollmentQueryHandler _handler;

    public GetEnrollmentQueryHandlerTests()
    {
        _enrollmentQueryService = Substitute.For<IEnrollmentQueryService>();
        _handler = new GetEnrollmentQueryHandler(_enrollmentQueryService);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenEnrollmentDoesNotExist()
    {
        // Arrange
        var enrollmentId = Guid.NewGuid();
        _enrollmentQueryService.GetEnrollmentByIdAsync(enrollmentId, Arg.Any<CancellationToken>())
            .Returns((EnrollmentDto?)null);

        var query = new GetEnrollmentQuery(enrollmentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.NotFound");
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Should_ReturnEnrollment_WhenFound()
    {
        // Arrange
        var enrollmentId = Guid.NewGuid();
        var dto = new EnrollmentDto(
            enrollmentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Active",
            3,
            4,
            75.5,
            DateTime.UtcNow,
            Guid.NewGuid());

        _enrollmentQueryService.GetEnrollmentByIdAsync(enrollmentId, Arg.Any<CancellationToken>())
            .Returns(dto);

        var query = new GetEnrollmentQuery(enrollmentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEquivalentTo(dto);
        await _enrollmentQueryService.Received(1).GetEnrollmentByIdAsync(enrollmentId, Arg.Any<CancellationToken>());
    }
}
