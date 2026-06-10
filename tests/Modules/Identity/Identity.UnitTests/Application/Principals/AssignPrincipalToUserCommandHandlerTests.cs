using AlphaZero.Modules.Identity.Application.Principals.Commands.AssignPrincipalToUser;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq.Expressions;

namespace AlphaZero.Modules.Identity.UnitTests.Application.Principals;

public class AssignPrincipalToUserCommandHandlerTests
{
    private readonly ITenantUserPrincpialAssignmentRepository _assignmentRepository;
    private readonly IRepository<TenantUser> _userRepository;
    private readonly IPrincipalRepository _principalRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AssignPrincipalToUserCommandHandler> _logger;
    private readonly AssignPrincipalToUserCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid PrincipalId = Guid.NewGuid();
    private static readonly string ResourceArn = "az:course:tenant:course/101";

    public AssignPrincipalToUserCommandHandlerTests()
    {
        _assignmentRepository = Substitute.For<ITenantUserPrincpialAssignmentRepository>();
        _userRepository = Substitute.For<IRepository<TenantUser>>();
        _principalRepository = Substitute.For<IPrincipalRepository>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _logger = Substitute.For<ILogger<AssignPrincipalToUserCommandHandler>>();
        _handler = new AssignPrincipalToUserCommandHandler(
            _assignmentRepository, _userRepository, _principalRepository, _tenantProvider, _logger);
    }

    [Fact]
    public async Task Handle_Should_CreateAssignment_WhenValid()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns(TenantId);
        _assignmentRepository.Any(Arg.Any<Expression<Func<TenantUserPrinciaplAssignment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var user = TenantUser.Create(TenantId, "sub", "Ali").Value;
        var principal = Principal.Create(PrincipalId, "role", "hash", "Role", PrincipalType.Role, null, TenantId).Value;

        _userRepository.GetById(UserId).Returns(user);
        _principalRepository.GetById(PrincipalId).Returns(principal);

        var command = new AssignPrincipalToUserCommand(UserId, PrincipalId, ResourceArn);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        _assignmentRepository.Received(1).Add(Arg.Is<TenantUserPrinciaplAssignment>(a => 
            a.TenantUser == user && a.Principal == principal && a.Resource.Value == ResourceArn.ToLowerInvariant()));
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenAssignmentAlreadyExists()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns(TenantId);
        _assignmentRepository.Any(Arg.Any<Expression<Func<TenantUserPrinciaplAssignment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new AssignPrincipalToUserCommand(UserId, PrincipalId, ResourceArn);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Assignment.Duplicate");
    }
}
