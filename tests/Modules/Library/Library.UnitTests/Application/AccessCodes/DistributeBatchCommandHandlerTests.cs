using AlphaZero.Modules.Library.Application.AccessCodes.DistributeBatch;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;

namespace Library.UnitTests.Application.AccessCodes;

public class DistributeBatchCommandHandlerTests
{
    private readonly IAccessCodeRepository _accessCodeRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<DistributeBatchCommandHandler> _logger;
    private readonly DistributeBatchCommandHandler _handler;

    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly ResourceArn TargetResource = ResourceArn.Create("az:course:tenant:course/101").Value;
    private static readonly JsonDocument Metadata = JsonDocument.Parse("{}");

    public DistributeBatchCommandHandlerTests()
    {
        _accessCodeRepository = Substitute.For<IAccessCodeRepository>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _tenantProvider.GetTenant().Returns(TenantId);
        _logger = Substitute.For<ILogger<DistributeBatchCommandHandler>>();

        _handler = new DistributeBatchCommandHandler(_accessCodeRepository, _tenantProvider, _logger);
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_WhenBatchActuallyUpdated()
    {
        // Arrange
        var code1 = AccessCode.Mint("h1", TenantId, Guid.NewGuid(), "s", TargetResource, Metadata, BatchId);
        var code2 = AccessCode.Mint("h2", TenantId, Guid.NewGuid(), "s", TargetResource, Metadata, BatchId);
        var codes = new List<AccessCode> { code1, code2 };

        _accessCodeRepository.GetBatchForUpdateAsync(TenantId, BatchId, Arg.Any<CancellationToken>())
            .Returns(codes);
        _accessCodeRepository.UpdateBatch(TenantId, BatchId, AccessCodeStatus.Minted, AccessCodeStatus.Distributed, Arg.Any<CancellationToken>())
            .Returns(2);

        var command = new DistributeBatchCommand(BatchId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        code1.Status.Should().Be(AccessCodeStatus.Distributed);
        code2.Status.Should().Be(AccessCodeStatus.Distributed);
        await _accessCodeRepository.Received(1).UpdateBatch(TenantId, BatchId, AccessCodeStatus.Minted, AccessCodeStatus.Distributed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenBatchDoesNotExist()
    {
        // Arrange
        _accessCodeRepository.GetBatchForUpdateAsync(TenantId, BatchId, Arg.Any<CancellationToken>())
            .Returns(new List<AccessCode>());
        _accessCodeRepository.BatchExistsAsync(BatchId, Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new DistributeBatchCommand(BatchId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Batch.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenBatchExists_ButNoCodesInMintedStatus()
    {
        // Arrange: batch has codes, but all already Distributed
        var code1 = AccessCode.Mint("h1", TenantId, Guid.NewGuid(), "s", TargetResource, Metadata, BatchId);
        code1.Distribute();
        var codes = new List<AccessCode> { code1 };

        _accessCodeRepository.GetBatchForUpdateAsync(TenantId, BatchId, Arg.Any<CancellationToken>())
            .Returns(codes);

        var command = new DistributeBatchCommand(BatchId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Conflict);
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
    }
}
