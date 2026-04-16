using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq.Expressions;

namespace AlphaZero.Modules.Identity.UnitTests.Application.Auth;

public class LoginAsTenantUserCommandHandlerTests
{
    private readonly IRepository<TenantUser> _userRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly ILogger<LoginAsTenantUserCommandHandler> _logger;
    private readonly LoginAsTenantUserCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly string IdentityId = "cognito-sub";

    public LoginAsTenantUserCommandHandlerTests()
    {
        _userRepository = Substitute.For<IRepository<TenantUser>>();
        _jwtProvider = Substitute.For<IJwtProvider>();
        _logger = Substitute.For<ILogger<LoginAsTenantUserCommandHandler>>();
        _handler = new LoginAsTenantUserCommandHandler(_userRepository, _logger, _jwtProvider);
    }

    [Fact]
    public async Task Handle_Should_ReturnToken_WhenUserIsEnrolled()
    {
        // Arrange
        var user = TenantUser.Create(TenantId, IdentityId, "Ali").Value;
        var initialSessionId = user.ActiveSessionId;

        _userRepository.ListAsync(Arg.Any<Expression<Func<TenantUser, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<TenantUser> { user });

        _jwtProvider.GenerateToken(user.Id, TenantId, Arg.Any<Guid>(), AuthorizationMethod.TenantUser)
            .Returns("token-123");

        var command = new LoginAsTenantUserCommand(IdentityId, TenantId, "Ali");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("token-123");
        result.Value.TenantUserId.Should().Be(user.Id);
        result.Value.SessionId.Should().NotBe(initialSessionId);
        
        _userRepository.Received(1).Update(Arg.Is<TenantUser>(u => u.ActiveSessionId == result.Value.SessionId));
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenUserIsNotEnrolled()
    {
        // Arrange
        _userRepository.ListAsync(Arg.Any<Expression<Func<TenantUser, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<TenantUser>());

        var command = new LoginAsTenantUserCommand(IdentityId, TenantId, "Ali");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.NotEnrolled");
    }
}
