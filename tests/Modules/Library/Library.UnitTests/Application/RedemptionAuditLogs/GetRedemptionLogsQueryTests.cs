using AlphaZero.Modules.Library.Application.Queries;
using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Shared.Queries;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Library.UnitTests.Application.RedemptionAuditLogs;

public class GetRedemptionLogsQueryTests
{
    private readonly ILibraryQueryService _queryService = Substitute.For<ILibraryQueryService>();
    private readonly GetRedemptionLogsQueryHandler _handler;
    private readonly GetRedemptionLogsQueryValidator _validator = new();

    public GetRedemptionLogsQueryTests()
    {
        _handler = new GetRedemptionLogsQueryHandler(_queryService);
    }

    [Fact]
    public async Task Validator_Should_Fail_WhenLibraryIdIsEmpty()
    {
        var query = new GetRedemptionLogsQuery(Guid.Empty, null, null);

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(query.LibraryId));
    }

    [Fact]
    public async Task Validator_Should_Pass_WhenLibraryIdIsProvided()
    {
        var query = new GetRedemptionLogsQuery(Guid.NewGuid(), null, null);

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_Should_CallQueryServiceAndReturnDtos()
    {
        // Arrange
        var libraryId = Guid.NewGuid();
        var accessCodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceArn = "az:course:tenant:course/math-101";

        var dto = new RedemptionAuditLogDto(
            Guid.NewGuid(),
            accessCodeId,
            libraryId,
            userId,
            "course-unlock",
            resourceArn,
            DateTime.UtcNow,
            "192.168.1.1",
            "fp-123");

        var pagedLogs = new PagedResult<RedemptionAuditLogDto>(
            new List<RedemptionAuditLogDto> { dto },
            TotalCount: 1,
            CurrentPage: 1,
            PageSize: 50);

        _queryService.GetRedemptionLogsAsync(
            libraryId,
            Arg.Any<DateOnly?>(),
            Arg.Any<DateOnly?>(),
            1,
            50,
            Arg.Any<CancellationToken>())
            .Returns(pagedLogs);

        var query = new GetRedemptionLogsQuery(libraryId, null, null, 1, 50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        var pagedResult = result.Value;
        pagedResult.TotalCount.Should().Be(1);
        pagedResult.Items.Should().HaveCount(1);

        var returnedDto = pagedResult.Items.First();
        returnedDto.Should().BeEquivalentTo(dto);

        await _queryService.Received(1).GetRedemptionLogsAsync(
            libraryId,
            null,
            null,
            1,
            50,
            Arg.Any<CancellationToken>());
    }
}
