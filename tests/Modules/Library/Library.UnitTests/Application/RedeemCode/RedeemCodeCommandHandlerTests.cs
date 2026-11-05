using AlphaZero.Modules.Library.Application.RedeemCode;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;

namespace Library.UnitTests.Application.RedeemCode;

public class RedeemCodeCommandHandlerTests
{
    private readonly IAccessCodeRepository _repository;
    private readonly IRedemptionAuditLogRepository _auditLogRepository;
    private readonly ISaleRecordRepository _saleRecordRepository;
    private readonly IRedemptionStrategyFactory _strategyFactory;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentTenantUserRepository _currentUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthorizationContextProvider _authorizationContextFactory;
    private readonly ILogger<RedeemCodeCommandHandler> _logger;
    private readonly RedeemCodeCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public RedeemCodeCommandHandlerTests()
    {
        _repository = Substitute.For<IAccessCodeRepository>();
        _auditLogRepository = Substitute.For<IRedemptionAuditLogRepository>();
        _saleRecordRepository = Substitute.For<ISaleRecordRepository>();
        _strategyFactory = Substitute.For<IRedemptionStrategyFactory>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _currentUserRepository = Substitute.For<ICurrentTenantUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _authorizationContextFactory = Substitute.For<IAuthorizationContextProvider>();
        _logger = Substitute.For<ILogger<RedeemCodeCommandHandler>>();

        _handler = new RedeemCodeCommandHandler(
            _repository,
            _auditLogRepository,
            _saleRecordRepository,
            _strategyFactory,
            _tenantProvider,
            _currentUserRepository,
            _passwordHasher,
            _authorizationContextFactory,
            _logger
            );
    }

    [Fact]
    public async Task Handle_Should_RedeemCode_WhenValid()
    {
        // Arrange
        var rawCode = "CODE-123";
        var hash = "hashed";
        var targetArn = ResourceArn.Create("az:course:tenant:course/101").Value;
        var accessCode = AccessCode.Mint(hash, TenantId, null, "strategy", targetArn, JsonDocument.Parse("{}"));
        accessCode.Distribute();

        _passwordHasher.HashPassword(rawCode).Returns(hash);
        _repository.GetByHashForUpdateAsync(hash, Arg.Any<CancellationToken>()).Returns(accessCode);
        _repository.UpdateAsync(accessCode, Arg.Any<CancellationToken>()).Returns(Result.Success);
        _tenantProvider.GetTenant().Returns(TenantId);
        _currentUserRepository.GetCurrentUser().Returns(new TenantUserDTO(UserId, Guid.NewGuid().ToString(),"User"));
        _authorizationContextFactory.CurrentAuthorizationContext.Returns(new AuthorizationContext()
        {
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString(),
            IpAddress = "127.0.0.1",
            DeviceId = "fingerprint",
            RequiredPermission=""
        });

        var strategy = Substitute.For<IRedemptionStrategy>();
        _strategyFactory.GetStrategy("strategy").Returns(strategy);

        var command = new RedeemCodeCommand(rawCode);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        accessCode.Status.Should().Be(AccessCodeStatus.Redeemed);
        await strategy.Received(1).ExecuteAsync(UserId, accessCode.Id, targetArn, Arg.Any<JsonElement>());
        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<RedemptionAuditLog>(log => 
                log.AccessCodeId == accessCode.Id && 
                log.IpAddress == "127.0.0.1" && 
                log.DeviceFingerprint == "fingerprint"), 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenUserNotAuthenticated_EvenWithForeignTenantCode()
    {
        // Arrange: code exists in another tenant, but caller is not authenticated
        var accessCode = AccessCode.Mint("h", TenantId, null, "s", ResourceArn.Create("az:course:tenant:course/101").Value, JsonDocument.Parse("{}"));
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("h");
        _repository.GetByHashForUpdateAsync("h", Arg.Any<CancellationToken>()).Returns(accessCode);
        _currentUserRepository.GetCurrentUser().Returns((TenantUserDTO?)null);
        _tenantProvider.GetTenant().Returns(Guid.NewGuid()); // Foreign tenant

        var command = new RedeemCodeCommand("C");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: 401 Unauthorized precedes 404 and 403
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.Unauthenticated");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCodeDoesNotExist_ForAuthenticatedUser()
    {
        // Arrange
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("missing");
        _repository.GetByHashForUpdateAsync("missing", Arg.Any<CancellationToken>()).Returns((AccessCode?)null);
        _currentUserRepository.GetCurrentUser().Returns(new TenantUserDTO(UserId, Guid.NewGuid().ToString(), "User"));
        _tenantProvider.GetTenant().Returns(TenantId);

        var command = new RedeemCodeCommand("MISSING");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: 404 Not Found precedes 403
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenTenantMismatch()
    {
        // Arrange
        var accessCode = AccessCode.Mint("h", TenantId, null, "s", ResourceArn.Create("az:course:tenant:course/101").Value, JsonDocument.Parse("{}"));
        _repository.GetByHashForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(accessCode);
        _currentUserRepository.GetCurrentUser().Returns(new TenantUserDTO(UserId, Guid.NewGuid().ToString(), "User"));
        _tenantProvider.GetTenant().Returns(Guid.NewGuid()); // Different tenant

        var command = new RedeemCodeCommand("C");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.TenantMismatch");
    }
}