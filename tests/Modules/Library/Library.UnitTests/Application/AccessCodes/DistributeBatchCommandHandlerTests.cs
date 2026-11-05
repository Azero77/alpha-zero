using AlphaZero.Modules.Library.Application.AccessCodes.DistributeBatch;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;

namespace Library.UnitTests.Application.AccessCodes;

public class DistributeBatchCommandHandlerTests
{
    private readonly IAccessCodeRepository _accessCodeRepository;
    private readonly ILogger<DistributeBatchCommandHandler> _logger;
    private readonly DistributeBatchCommandHandler _handler;

    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly ResourceArn TargetResource = ResourceArn.Create("az:course:tenant:course/101").Value;
    private static readonly JsonDocument Metadata = JsonDocument.Parse("{}");

    public DistributeBatchCommandHandlerTests()
    {
        _accessCodeRepository = Substitute.For<IAccessCodeRepository>();
        _logger = Substitute.For<ILogger<DistributeBatchCommandHandler>>();

        _handler = new DistributeBatchCommandHandler(_accessCodeRepository, _logger);
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_WhenBatchActuallyUpdated()
    {
        // Arrange
        var code1 = AccessCode.Mint("h1", TenantId, Guid.NewGuid(), "s", TargetResource, Metadata, BatchId);
        var code2 = AccessCode.Mint("h2", TenantId, Guid.NewGuid(), "s", TargetResource, Metadata, BatchId);
        var codes = new List<AccessCode> { code1, code2 };

        _accessCodeRepository.GetBatchForUpdateAsync(BatchId, Arg.Any<CancellationToken>())
            .Returns(codes);
        _accessCodeRepository.UpdateAsync(Arg.Any<AccessCode>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);

        var command = new DistributeBatchCommand(BatchId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        code1.Status.Should().Be(AccessCodeStatus.Distributed);
        code2.Status.Should().Be(AccessCodeStatus.Distributed);
        await _accessCodeRepository.Received(2).UpdateAsync(Arg.Any<AccessCode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenBatchDoesNotExist()
    {
        // Arrange
        _accessCodeRepository.GetBatchForUpdateAsync(BatchId, Arg.Any<CancellationToken>())
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

        _accessCodeRepository.GetBatchForUpdateAsync(BatchId, Arg.Any<CancellationToken>())
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
