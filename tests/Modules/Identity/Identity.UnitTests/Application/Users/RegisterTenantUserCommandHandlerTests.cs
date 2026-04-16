using AlphaZero.Modules.Identity.Application.Users.Commands.RegisterTenantUser;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq.Expressions;

namespace AlphaZero.Modules.Identity.UnitTests.Application.Users;

public class RegisterTenantUserCommandHandlerTests
{
    private readonly IRepository<TenantUser> _userRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<RegisterTenantUserCommandHandler> _logger;
    private readonly RegisterTenantUserCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly string IdentityId = "cognito-sub";

    public RegisterTenantUserCommandHandlerTests()
    {
        _userRepository = Substitute.For<IRepository<TenantUser>>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _logger = Substitute.For<ILogger<RegisterTenantUserCommandHandler>>();
        _handler = new RegisterTenantUserCommandHandler(_userRepository, _tenantProvider, _logger);
    }

    [Fact]
    public async Task Handle_Should_RegisterUser_WhenNotAlreadyExists()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns(TenantId);
        _userRepository.Any(Arg.Any<Expression<Func<TenantUser, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new RegisterTenantUserCommand(IdentityId, "Ali");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        _userRepository.Received(1).Add(Arg.Is<TenantUser>(u => u.IdentityId == IdentityId && u.TenantId == TenantId));
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenUserAlreadyRegistered()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns(TenantId);
        _userRepository.Any(Arg.Any<Expression<Func<TenantUser, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new RegisterTenantUserCommand(IdentityId, "Ali");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.AlreadyRegistered");
    }
}
