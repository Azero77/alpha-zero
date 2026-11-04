using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Queries;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Library.UnitTests.Application.RedemptionAuditLogs;

public class GetRedemptionLogsQueryTests
{
    private readonly IRedemptionAuditLogRepository _repository = Substitute.For<IRedemptionAuditLogRepository>();
    private readonly GetRedemptionLogsQueryHandler _handler;
    private readonly GetRedemptionLogsQueryValidator _validator = new();

    public GetRedemptionLogsQueryTests()
    {
        _handler = new GetRedemptionLogsQueryHandler(_repository);
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
    public async Task Handler_Should_CallRepositoryAndMapToDtos()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var accessCodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceArn = ResourceArn.Create("course", tenantId.ToString(), "math-101").Value;
        var context = new RedemptionContext(
            tenantId,
            libraryId,
            userId,
            "course-unlock",
            resourceArn,
            DateTime.UtcNow,
            "192.168.1.1",
            "fp-123");
        var log = RedemptionAuditLog.Record(context, accessCodeId);

        var pagedLogs = new PagedResult<RedemptionAuditLog>(
            new List<RedemptionAuditLog> { log },
            TotalCount: 1,
            CurrentPage: 1,
            PageSize: 50);

        _repository.GetPagedAsync(
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

        var dto = pagedResult.Items.First();
        dto.Id.Should().Be(log.Id);
        dto.AccessCodeId.Should().Be(accessCodeId);
        dto.LibraryId.Should().Be(libraryId);
        dto.RedeemedByUserId.Should().Be(userId);
        dto.StrategyId.Should().Be("course-unlock");
        dto.TargetResourceArn.Should().Be(resourceArn.Value);
        dto.RedeemedAt.Should().Be(log.RedeemedAt);
        dto.IpAddress.Should().Be("192.168.1.1");
        dto.DeviceFingerprint.Should().Be("fp-123");

        await _repository.Received(1).GetPagedAsync(
            libraryId,
            null,
            null,
            1,
            50,
            Arg.Any<CancellationToken>());
    }
}
