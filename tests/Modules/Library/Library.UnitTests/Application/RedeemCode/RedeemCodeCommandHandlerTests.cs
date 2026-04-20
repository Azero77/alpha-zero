using AlphaZero.Modules.Library.Application.RedeemCode;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using FluentAssertions;
using NSubstitute;
using System.Text.Json;

namespace Library.UnitTests.Application.RedeemCode;

public class RedeemCodeCommandHandlerTests
{
    private readonly IAccessCodeRepository _repository;
    private readonly IRedemptionStrategyFactory _strategyFactory;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentTenantUserRepository _currentUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly RedeemCodeCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public RedeemCodeCommandHandlerTests()
    {
        _repository = Substitute.For<IAccessCodeRepository>();
        _strategyFactory = Substitute.For<IRedemptionStrategyFactory>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _currentUserRepository = Substitute.For<ICurrentTenantUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();

        _handler = new RedeemCodeCommandHandler(
            _repository,
            _strategyFactory,
            _tenantProvider,
            _currentUserRepository,
            _passwordHasher);
    }

    [Fact]
    public async Task Handle_Should_RedeemCode_WhenValid()
    {
        // Arrange
        var rawCode = "CODE-123";
        var hash = "hashed";
        var targetArn = ResourceArn.Create("az:courses:tenant:course/101").Value;
        var accessCode = AccessCode.Mint(hash, TenantId, null, "strategy", targetArn, JsonDocument.Parse("{}"));

        _passwordHasher.HashPassword(rawCode).Returns(hash);
        _repository.GetByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(accessCode);
        _tenantProvider.GetTenant().Returns(TenantId);
        _currentUserRepository.GetCurrentUser().Returns(new TenantUserDTO(UserId, Guid.NewGuid().ToString(),"User",Guid.NewGuid()));
        
        var strategy = Substitute.For<IRedemptionStrategy>();
        _strategyFactory.GetStrategy("strategy").Returns(strategy);

        var command = new RedeemCodeCommand(rawCode);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        accessCode.Status.Should().Be(AccessCodeStatus.Redeemed);
        await strategy.Received(1).ExecuteAsync(UserId, accessCode.Id, targetArn, Arg.Any<JsonElement>());
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenTenantMismatch()
    {
        // Arrange
        var accessCode = AccessCode.Mint("h", TenantId, null, "s", ResourceArn.Create("az:courses:tenant:course/101").Value, JsonDocument.Parse("{}"));
        _repository.GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(accessCode);
        _tenantProvider.GetTenant().Returns(Guid.NewGuid()); // Different tenant

        var command = new RedeemCodeCommand("C");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AccessCode.TenantMismatch");
    }
}