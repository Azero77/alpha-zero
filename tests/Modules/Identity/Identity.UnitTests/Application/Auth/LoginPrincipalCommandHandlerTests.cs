using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginPrincipal;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq.Expressions;

namespace AlphaZero.Modules.Identity.UnitTests.Application.Auth;

public class LoginPrincipalCommandHandlerTests
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly IJwtProvider _jwtProvider;
    private readonly ILogger<LoginPrincipalCommandHandler> _logger;
    private readonly IPasswordHasher _passwordHasher;
    private readonly PrincipalLoginService _principalLoginService;
    private readonly IClock _clock;
    private readonly LoginPrincipalCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PrincipalId = Guid.NewGuid();

    public LoginPrincipalCommandHandlerTests()
    {
        _principalRepository = Substitute.For<IPrincipalRepository>();
        _jwtProvider = Substitute.For<IJwtProvider>();
        _logger = Substitute.For<ILogger<LoginPrincipalCommandHandler>>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _principalLoginService = new PrincipalLoginService(_passwordHasher);
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(DateTime.UtcNow);

        _handler = new LoginPrincipalCommandHandler(
            _principalRepository,
            _jwtProvider,
            _principalLoginService,
            _clock,
            _logger);
    }

    [Fact]
    public async Task Handle_Should_GenerateTokenWithPrincipalClaims_WhenCredentialsAreValid()
    {
        // Arrange
        var password = "securePassword123";
        var passwordHash = "hashedPassword";
        _passwordHasher.VerifyPassword(password, passwordHash).Returns(true);

        var principal = Principal.Create(
            PrincipalId,
            "admin_user",
            passwordHash,
            "Admin User",
            PrincipalType.User,
            "az:course:*:*",
            TenantId).Value;

        _principalRepository.GetFirst(Arg.Any<Expression<Func<Principal, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(principal);

        _jwtProvider.GenerateToken(principal.Id, TenantId, AuthenticationMethod.Principal, Arg.Any<List<ClaimDTO>>())
            .Returns("principal-token-123");

        var command = new LoginPrincipalCommand(TenantId, "admin_user", password);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("principal-token-123");
        result.Value.TenantUserId.Should().Be(PrincipalId);

        _jwtProvider.Received(1).GenerateToken(
            PrincipalId,
            TenantId,
            AuthenticationMethod.Principal,
            Arg.Is<List<ClaimDTO>>(claims =>
                claims.Any(c => c.Key == CustomClaimTypes.Name && c.Value == "Admin User") &&
                claims.Any(c => c.Key == CustomClaimTypes.Username && c.Value == "admin_user") &&
                claims.Any(c => c.Key == CustomClaimTypes.PrincipalType && c.Value == PrincipalType.User.ToString()) &&
                claims.Any(c => c.Key == CustomClaimTypes.IsManaged && c.Value == "false") &&
                claims.Any(c => c.Key == CustomClaimTypes.IsGlobal && c.Value == "false") &&
                claims.Any(c => c.Key == CustomClaimTypes.PrincipalScope && c.Value == "az:course:*:*")
            ));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenPrincipalNotFound()
    {
        // Arrange
        _principalRepository.GetFirst(Arg.Any<Expression<Func<Principal, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Principal?)null);

        var command = new LoginPrincipalCommand(TenantId, "nonexistent", "password");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.NotFoundCredentials");
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenPasswordIsInvalid()
    {
        // Arrange
        var passwordHash = "hashedPassword";
        _passwordHasher.VerifyPassword("wrongPassword", passwordHash).Returns(false);

        var principal = Principal.Create(
            PrincipalId,
            "admin_user",
            passwordHash,
            "Admin User",
            PrincipalType.User,
            null,
            TenantId).Value;

        _principalRepository.GetFirst(Arg.Any<Expression<Func<Principal, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(principal);

        var command = new LoginPrincipalCommand(TenantId, "admin_user", "wrongPassword");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidCredentials");
    }
}
