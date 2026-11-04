using AlphaZero.Modules.Library.Application.AccessCodes.DistributeBatch;
using AlphaZero.Modules.Library.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Library.UnitTests.Application.AccessCodes;

public class DistributeBatchCommandHandlerTests
{
    private readonly IAccessCodeRepository _accessCodeRepository;
    private readonly ILogger<DistributeBatchCommandHandler> _logger;
    private readonly DistributeBatchCommandHandler _handler;

    private static readonly Guid BatchId = Guid.NewGuid();

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
        _accessCodeRepository.MarkBatchAsDistributedAsync(BatchId, Arg.Any<CancellationToken>())
            .Returns(3);

        var command = new DistributeBatchCommand(BatchId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenBatchDoesNotExist()
    {
        // Arrange: the bulk update matched zero rows and the batch does not exist at all
        _accessCodeRepository.MarkBatchAsDistributedAsync(BatchId, Arg.Any<CancellationToken>())
            .Returns(0);
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
    public async Task Handle_Should_NotReturnSuccess_WhenZeroRowsUpdated()
    {
        // Arrange: zero-row update must never surface as success (silent no-op invariant)
        _accessCodeRepository.MarkBatchAsDistributedAsync(BatchId, Arg.Any<CancellationToken>())
            .Returns(0);
        _accessCodeRepository.BatchExistsAsync(BatchId, Arg.Any<CancellationToken>())
            .Returns(true); // batch exists but nothing was in Minted status

        var command = new DistributeBatchCommand(BatchId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Conflict);
    }
}
