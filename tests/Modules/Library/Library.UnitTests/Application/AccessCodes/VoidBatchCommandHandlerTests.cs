using System.Text.Json;
using AlphaZero.Modules.Library.Application.AccessCodes.VoidBatch;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Library.UnitTests.Application.AccessCodes;

public class VoidBatchCommandHandlerTests
{
    private readonly IAccessCodeRepository _repository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<VoidBatchCommandHandler> _logger;
    private readonly ICurrentTenantUserRepository _currentUserRepository;
    private readonly IPolicyEvaluatorService _policyEvaluatorService;
    private readonly IAuthorizationContextProvider _authContextProvider;
    private readonly VoidBatchCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid LibraryId = Guid.NewGuid();
    private static readonly Guid ActorUserId = Guid.NewGuid();
    private static readonly ResourceArn TargetResource = ResourceArn.Create("az:course:tenant:course/101").Value;
    private static readonly JsonDocument EmptyMetadata = JsonDocument.Parse("{}");

    public VoidBatchCommandHandlerTests()
    {
        _repository = Substitute.For<IAccessCodeRepository>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _logger = Substitute.For<ILogger<VoidBatchCommandHandler>>();
        _currentUserRepository = Substitute.For<ICurrentTenantUserRepository>();
        _policyEvaluatorService = Substitute.For<IPolicyEvaluatorService>();
        _authContextProvider = Substitute.For<IAuthorizationContextProvider>();

        _tenantProvider.GetTenant().Returns(TenantId);

        var tenantUser = new TenantUserDTO(ActorUserId, "identity", "Test User", TenantId);
        _currentUserRepository.GetCurrentUser().Returns(tenantUser);

        _policyEvaluatorService.Authorize(Arg.Any<AuthorizationContext>())
            .Returns(Result.Success);

        _repository.UpdateAsync(Arg.Any<AccessCode>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);

        _handler = new VoidBatchCommandHandler(
            _repository,
            _tenantProvider,
            _logger,
            _currentUserRepository,
            _policyEvaluatorService,
            _authContextProvider);
    }

    [Fact]
    public async Task Handle_WhenBatchDoesNotExist_Should_ReturnNotFound()
    {
        var batchId = Guid.NewGuid();
        _repository.CountBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(0);
        _repository.BatchExistsAsync(batchId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new VoidBatchCommand(batchId, "Compromised"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Batch.NotFound");
    }

    [Fact]
    public async Task Handle_WhenBatchOverCap_Should_ReturnValidationError_WithZeroCodesTouched()
    {
        var batchId = Guid.NewGuid();
        _repository.CountBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(501);

        var result = await _handler.Handle(new VoidBatchCommand(batchId, "Compromised"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Batch.OverCap");
        await _repository.DidNotReceive().GetBatchForUpdateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<AccessCode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MixedBatch_Should_VoidVoidableCodes_AndSkipTerminalCodes()
    {
        var batchId = Guid.NewGuid();
        var redeemedUserId = Guid.NewGuid();

        var mintedCode = AccessCode.Mint("h1", TenantId, LibraryId, "strategy", TargetResource, EmptyMetadata, batchId);
        
        var distributedCode = AccessCode.Mint("h2", TenantId, LibraryId, "strategy", TargetResource, EmptyMetadata, batchId);
        distributedCode.Distribute();

        var soldCode = AccessCode.Mint("h3", TenantId, LibraryId, "strategy", TargetResource, EmptyMetadata, batchId);
        soldCode.Distribute();
        soldCode.Sell(LibraryId, ActorUserId, 50m, "USD");

        var redeemedCode = AccessCode.Mint("h4", TenantId, LibraryId, "strategy", TargetResource, EmptyMetadata, batchId);
        redeemedCode.Distribute();
        redeemedCode.Redeem(redeemedUserId);

        var alreadyVoidedCode = AccessCode.Mint("h5", TenantId, LibraryId, "strategy", TargetResource, EmptyMetadata, batchId);
        alreadyVoidedCode.Void("Previously voided");

        var expiredCode = AccessCode.Mint("h6", TenantId, LibraryId, "strategy", TargetResource, EmptyMetadata, batchId);
        expiredCode.Expire();

        var codes = new List<AccessCode> { mintedCode, distributedCode, soldCode, redeemedCode, alreadyVoidedCode, expiredCode };
        _repository.CountBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(6);
        _repository.GetBatchForUpdateAsync(TenantId, batchId, Arg.Any<CancellationToken>()).Returns(codes);

        var result = await _handler.Handle(new VoidBatchCommand(batchId, "Compromised inventory"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCodes.Should().Be(6);
        result.Value.VoidedCount.Should().Be(4);
        result.Value.SkippedCount.Should().Be(2);
        result.Value.VoidedCodeIds.Should().BeEquivalentTo(new[] { mintedCode.Id, distributedCode.Id, soldCode.Id, redeemedCode.Id });

        // Revocation of the redeemed code is producer-only (#58): the domain event carries
        // the redeemed user for AccessCodeVoidedDomainEventHandler; asserted in domain tests.

        // 4 codes updated via repository
        await _repository.Received(4).UpdateAsync(Arg.Any<AccessCode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReRunOnAlreadyVoidedBatch_Should_BeIdempotent_SkippingAll()
    {
        var batchId = Guid.NewGuid();
        var code1 = AccessCode.Mint("h1", TenantId, LibraryId, "strategy", TargetResource, EmptyMetadata, batchId);
        code1.Void("Recall 1");
        var code2 = AccessCode.Mint("h2", TenantId, LibraryId, "strategy", TargetResource, EmptyMetadata, batchId);
        code2.Void("Recall 1");

        var codes = new List<AccessCode> { code1, code2 };
        _repository.CountBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(2);
        _repository.GetBatchForUpdateAsync(TenantId, batchId, Arg.Any<CancellationToken>()).Returns(codes);

        var result = await _handler.Handle(new VoidBatchCommand(batchId, "Recall 2"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCodes.Should().Be(2);
        result.Value.VoidedCount.Should().Be(0);
        result.Value.SkippedCount.Should().Be(2);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<AccessCode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ForeignTenantBatch_Should_ReturnNotFound_AndNeverLockOrVoid()
    {
        // The tenant predicate is part of the FOR UPDATE statement (I-TENANT): rows of another
        // tenant are never locked or read, so a foreign batch reads back as not found. The stub
        // mirrors exactly what the tenant-scoped SQL guarantees: the tenant filter is passed to
        // the locking query and no foreign code is ever handed to the handler.
        var batchId = Guid.NewGuid();

        _repository.CountBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(0); // tenant-scoped count
        _repository.BatchExistsAsync(batchId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new VoidBatchCommand(batchId, "Compromised"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Batch.NotFound");

        // The foreign batch is invisible in every tenant-scoped read, so the handler short-
        // circuits before any FOR UPDATE statement: cross-tenant rows are never locked, voided,
        // or even enumerated.
        await _repository.DidNotReceive().GetBatchForUpdateAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<AccessCode>(), Arg.Any<CancellationToken>());
    }
}
