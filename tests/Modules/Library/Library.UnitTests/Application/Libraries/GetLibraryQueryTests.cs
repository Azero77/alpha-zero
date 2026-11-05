using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Modules.Library.Application.Queries;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Library.UnitTests.Application.Libraries;

public class GetLibraryQueryTests
{
    private readonly ILibraryQueryService _queryService = Substitute.For<ILibraryQueryService>();
    private readonly GetLibraryQueryHandler _handler;

    public GetLibraryQueryTests()
    {
        _handler = new GetLibraryQueryHandler(_queryService);
    }

    [Fact]
    public async Task Handle_Should_ReturnLibraryDto_WhenLibraryExists()
    {
        // Arrange
        var libraryId = Guid.NewGuid();
        var dto = new LibraryDto(libraryId, "Test Lib", "Damascus", "12345", ["az:course:tenant:course/c1"]);
        _queryService.GetLibraryByIdAsync(libraryId, Arg.Any<CancellationToken>()).Returns(dto);

        var query = new GetLibraryQuery(libraryId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(dto);
        await _queryService.Received(1).GetLibraryByIdAsync(libraryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenLibraryDoesNotExist()
    {
        // Arrange
        var libraryId = Guid.NewGuid();
        _queryService.GetLibraryByIdAsync(libraryId, Arg.Any<CancellationToken>()).Returns((LibraryDto?)null);

        var query = new GetLibraryQuery(libraryId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Library.NotFound");
        await _queryService.Received(1).GetLibraryByIdAsync(libraryId, Arg.Any<CancellationToken>());
    }
}
