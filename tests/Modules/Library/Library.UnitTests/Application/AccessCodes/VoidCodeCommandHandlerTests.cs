using System.Text.Json;
using AlphaZero.Modules.Library.Application.AccessCodes.VoidCode;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Library.UnitTests.Application.AccessCodes;

public class VoidCodeCommandHandlerTests
{
    private readonly IAccessCodeRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<VoidCodeCommandHandler> _logger;
    private readonly VoidCodeCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public VoidCodeCommandHandlerTests()
    {
        _repository = Substitute.For<IAccessCodeRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _logger = Substitute.For<ILogger<VoidCodeCommandHandler>>();

        _handler = new VoidCodeCommandHandler(
            _repository,
            _passwordHasher,
            _tenantProvider,
            _logger);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCodeDoesNotExist()
    {
        // Arrange
        _passwordHasher.HashPassword("RAW-CODE").Returns("hash");
        _repository.GetByHashForUpdateAsync("hash", Arg.Any<CancellationToken>()).Returns((AccessCode?)null);
        _tenantProvider.GetTenant().Returns(TenantId);

        var command = new VoidCodeCommand("RAW-CODE", "Lost");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("AccessCode.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenTenantMismatch()
    {
        // Arrange: code belongs to TenantA, caller is TenantB
        var foreignTenantId = Guid.NewGuid();
        var targetArn = ResourceArn.Create("az:course:tenant:course/101").Value;
        var code = AccessCode.Mint("hash", foreignTenantId, null, "strategy", targetArn, JsonDocument.Parse("{}"));

        _passwordHasher.HashPassword("RAW-CODE").Returns("hash");
        _repository.GetByHashForUpdateAsync("hash", Arg.Any<CancellationToken>()).Returns(code);
        _tenantProvider.GetTenant().Returns(TenantId); // Current tenant differs from foreignTenantId

        var command = new VoidCodeCommand("RAW-CODE", "Attempted cross-tenant void");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Forbidden);
        result.FirstError.Code.Should().Be("AccessCode.TenantMismatch");
        result.FirstError.Description.Should().Be("This code belongs to another academy.");
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenSameTenant()
    {
        // Arrange
        var targetArn = ResourceArn.Create("az:course:tenant:course/101").Value;
        var code = AccessCode.Mint("hash", TenantId, null, "strategy", targetArn, JsonDocument.Parse("{}"));

        _passwordHasher.HashPassword("RAW-CODE").Returns("hash");
        _repository.GetByHashForUpdateAsync("hash", Arg.Any<CancellationToken>()).Returns(code);
        _repository.UpdateAsync(code, Arg.Any<CancellationToken>()).Returns(Result.Success);
        _tenantProvider.GetTenant().Returns(TenantId);

        var command = new VoidCodeCommand("RAW-CODE", "Damaged card");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        code.Status.Should().Be(AccessCodeStatus.Voided);
        code.VoidedReason.Should().Be("Damaged card");
        await _repository.Received(1).UpdateAsync(code, Arg.Any<CancellationToken>());
    }
}
