using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetAccessCodesProvisioningState;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

public class GetAccessCodesProvisioningStateQueryHandlerTests
{
    private readonly IFailedProvisioningRecordRepository _failedProvisioningRepository;
    private readonly ILogger<GetAccessCodesProvisioningStateQueryHandler> _logger;
    private readonly GetAccessCodesProvisioningStateQueryHandler _handler;

    public GetAccessCodesProvisioningStateQueryHandlerTests()
    {
        _logger = Substitute.For<ILogger<GetAccessCodesProvisioningStateQueryHandler>>();
        _failedProvisioningRepository = Substitute.For<IFailedProvisioningRecordRepository>();
        _handler = new GetAccessCodesProvisioningStateQueryHandler(_failedProvisioningRepository, _logger);
    }

    [Fact]
    public async Task Handle_Should_MatchRepositoryOnTenantAndRequestedCodes_AndMapDomainRecords()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var codeId1 = Guid.NewGuid();
        var codeId2 = Guid.NewGuid();
        var requested = new[] { codeId1, codeId2 };

        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastAttemptedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var userId = Guid.NewGuid();

        var record = new FailedProvisioningRecord(
            Guid.NewGuid(),
            tenantId,
            codeId1,
            userId,
            "arn:az:course:somewhere",
            "Course not found",
            ProvisioningStatus.Failed,
            3,
            createdAt,
            lastAttemptedAt);

        _failedProvisioningRepository.GetUnprovisionedByTenantAndAccessCodesAsync(
                tenantId, Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(requested)), Arg.Any<CancellationToken>())
            .Returns(new[] { record });

        var query = new GetAccessCodesProvisioningStateQuery(tenantId, requested);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);

        var item = result.Value[0];
        item.AccessCodeId.Should().Be(codeId1);
        item.UserId.Should().Be(userId);
        item.TargetResourceArn.Should().Be("arn:az:course:somewhere");
        item.Status.Should().Be("Failed");
        item.FailureReason.Should().Be("Course not found");
        item.RetryCount.Should().Be(3);
        item.CreatedAt.Should().Be(createdAt);
        item.LastAttemptedAt.Should().Be(lastAttemptedAt);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmpty_WhenNoRecordsMatch()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var codeId = Guid.NewGuid();

        _failedProvisioningRepository.GetUnprovisionedByTenantAndAccessCodesAsync(
                tenantId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FailedProvisioningRecord>());

        var query = new GetAccessCodesProvisioningStateQuery(tenantId, new[] { codeId });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
