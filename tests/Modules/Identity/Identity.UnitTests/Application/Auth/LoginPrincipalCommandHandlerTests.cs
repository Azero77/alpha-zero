using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginPrincipal;
using AlphaZero.Modules.Identity.Domain.Models;
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
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly ILogger<LoginPrincipalCommandHandler> _logger;
    private readonly LoginPrincipalCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly string Username = "iam-user";
    private static readonly string Password = "secure-password";
    private static readonly string PasswordHash = "hashed-password";

    public LoginPrincipalCommandHandlerTests()
    {
        _principalRepository = Substitute.For<IPrincipalRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtProvider = Substitute.For<IJwtProvider>();
        _logger = Substitute.For<ILogger<LoginPrincipalCommandHandler>>();
        _handler = new LoginPrincipalCommandHandler(_principalRepository, _passwordHasher, _jwtProvider, _logger);
    }

    [Fact]
    public async Task Handle_Should_ReturnToken_WhenCredentialsAreValid()
    {
        // Arrange
        var principal = Principal.Create(Guid.NewGuid(), Username, PrincipalType.User, TenantId, null, "IAM User", PasswordHash).Value;
        
        _principalRepository.ListAsync(Arg.Any<Expression<Func<Principal, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Principal> { principal });

        _passwordHasher.VerifyPassword(Password, PasswordHash).Returns(true);

        _jwtProvider.GenerateToken(principal.Id, TenantId, Arg.Any<Guid>(), AuthorizationMethod.Principal)
            .Returns("token-principal");

        var command = new LoginPrincipalCommand(TenantId, Username, Password);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("token-principal");
        result.Value.TenantUserId.Should().Be(principal.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenUsernameIsInvalid()
    {
        // Arrange
        _principalRepository.ListAsync(Arg.Any<Expression<Func<Principal, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Principal>());

        var command = new LoginPrincipalCommand(TenantId, "wrong-user", Password);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenPasswordIsInvalid()
    {
        // Arrange
        var principal = Principal.Create(Guid.NewGuid(), Username, PrincipalType.User, TenantId, null, "IAM User", PasswordHash).Value;

        _principalRepository.ListAsync(Arg.Any<Expression<Func<Principal, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Principal> { principal });

        _passwordHasher.VerifyPassword("wrong-password", PasswordHash).Returns(false);

        var command = new LoginPrincipalCommand(TenantId, Username, "wrong-password");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidCredentials");
    }
}
