using System.Text.Json;
using AlphaZero.Modules.Library.Application.AccessCodes.SellCode;
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

public class SellCodeCommandHandlerTests
{
    private readonly IAccessCodeRepository _accessCodeRepository;
    private readonly ISaleRecordRepository _saleRecordRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentTenantUserRepository _currentUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPolicyEvaluatorService _policyEvaluatorService;
    private readonly ILogger<SellCodeCommandHandler> _logger;
    private readonly SellCodeCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid LibraryId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public SellCodeCommandHandlerTests()
    {
        _accessCodeRepository = Substitute.For<IAccessCodeRepository>();
        _saleRecordRepository = Substitute.For<ISaleRecordRepository>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _currentUserRepository = Substitute.For<ICurrentTenantUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _policyEvaluatorService = Substitute.For<IPolicyEvaluatorService>();
        _logger = Substitute.For<ILogger<SellCodeCommandHandler>>();

        _policyEvaluatorService.Authorize(Arg.Any<AuthorizationContext>())
            .Returns(Task.FromResult<ErrorOr<Success>>(Result.Success));

        _handler = new SellCodeCommandHandler(
            _accessCodeRepository,
            _saleRecordRepository,
            _tenantProvider,
            _currentUserRepository,
            _passwordHasher,
            _logger,
            _policyEvaluatorService);
    }

    [Fact]
    public async Task Handle_Should_SellCode_WhenValid_AndSnapshotFaceValueAndCurrency()
    {
        // Arrange
        var rawCode = "SOLD-123";
        var hash = "hashed-code";
        var metadata = JsonDocument.Parse("{\"FaceValue\": 75.50, \"Currency\": \"EUR\"}");
        var targetArn = ResourceArn.Create("az:course:tenant:course/101").Value;

        var code = AccessCode.Mint(hash, TenantId, LibraryId, "strategy", targetArn, metadata);
        code.Distribute();
        code.PopDomainEvents();

        _passwordHasher.HashPassword(rawCode).Returns(hash);
        _accessCodeRepository.GetByHashForUpdateAsync(hash, Arg.Any<CancellationToken>()).Returns(code);
        _accessCodeRepository.UpdateAsync(code, Arg.Any<CancellationToken>()).Returns(Result.Success);
        _saleRecordRepository.ExistsForCodeAsync(code.Id, Arg.Any<CancellationToken>()).Returns(false);
        _tenantProvider.GetTenant().Returns(TenantId);
        _currentUserRepository.GetCurrentUser().Returns(new TenantUserDTO(UserId, Guid.NewGuid().ToString(), "Seller"));

        var command = new SellCodeCommand(rawCode, LibraryId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.AccessCodeId.Should().Be(code.Id);
        result.Value.LibraryId.Should().Be(LibraryId);
        result.Value.Price.Should().Be(75.50m);
        result.Value.Currency.Should().Be("EUR");

        code.Status.Should().Be(AccessCodeStatus.Sold);
        await _saleRecordRepository.Received(1).AddAsync(
            Arg.Is<SaleRecord>(s =>
                s.AccessCodeId == code.Id &&
                s.LibraryId == LibraryId &&
                s.Price == 75.50m &&
                s.Currency == "EUR" &&
                s.SoldByUserId == UserId),
            Arg.Any<CancellationToken>());
        await _accessCodeRepository.Received(1).UpdateAsync(code, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        _currentUserRepository.GetCurrentUser().Returns((TenantUserDTO?)null);
        var command = new SellCodeCommand("RAW-CODE");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.Unauthenticated");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCodeDoesNotExist()
    {
        // Arrange
        _currentUserRepository.GetCurrentUser().Returns(new TenantUserDTO(UserId, Guid.NewGuid().ToString(), "Seller"));
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("missing-hash");
        _accessCodeRepository.GetByHashForUpdateAsync("missing-hash", Arg.Any<CancellationToken>()).Returns((AccessCode?)null);

        var command = new SellCodeCommand("MISSING-CODE");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenTenantMismatch()
    {
        // Arrange
        var targetArn = ResourceArn.Create("az:course:tenant:course/101").Value;
        var code = AccessCode.Mint("h", TenantId, LibraryId, "strategy", targetArn, JsonDocument.Parse("{}"));
        _currentUserRepository.GetCurrentUser().Returns(new TenantUserDTO(UserId, Guid.NewGuid().ToString(), "Seller"));
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("h");
        _accessCodeRepository.GetByHashForUpdateAsync("h", Arg.Any<CancellationToken>()).Returns(code);
        _tenantProvider.GetTenant().Returns(Guid.NewGuid()); // Foreign tenant

        var command = new SellCodeCommand("CODE");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.TenantMismatch");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenCodeIsMintedAndNotDistributed()
    {
        // Arrange
        var targetArn = ResourceArn.Create("az:course:tenant:course/101").Value;
        var code = AccessCode.Mint("h", TenantId, LibraryId, "strategy", targetArn, JsonDocument.Parse("{}"));
        _currentUserRepository.GetCurrentUser().Returns(new TenantUserDTO(UserId, Guid.NewGuid().ToString(), "Seller"));
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("h");
        _accessCodeRepository.GetByHashForUpdateAsync("h", Arg.Any<CancellationToken>()).Returns(code);
        _tenantProvider.GetTenant().Returns(TenantId);

        var command = new SellCodeCommand("CODE", LibraryId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.InvalidStatus");
        code.Status.Should().Be(AccessCodeStatus.Minted);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenCodeIsAlreadySold()
    {
        // Arrange
        var targetArn = ResourceArn.Create("az:course:tenant:course/101").Value;
        var code = AccessCode.Mint("h", TenantId, LibraryId, "strategy", targetArn, JsonDocument.Parse("{}"));
        code.Distribute();
        code.Sell(LibraryId, UserId, 10m, "USD");
        code.PopDomainEvents();

        _currentUserRepository.GetCurrentUser().Returns(new TenantUserDTO(UserId, Guid.NewGuid().ToString(), "Seller"));
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("h");
        _accessCodeRepository.GetByHashForUpdateAsync("h", Arg.Any<CancellationToken>()).Returns(code);
        _tenantProvider.GetTenant().Returns(TenantId);
        _saleRecordRepository.ExistsForCodeAsync(code.Id, Arg.Any<CancellationToken>()).Returns(true);

        var command = new SellCodeCommand("CODE", LibraryId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.AlreadySold");
    }
}
