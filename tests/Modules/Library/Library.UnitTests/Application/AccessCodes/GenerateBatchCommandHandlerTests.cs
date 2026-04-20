using AlphaZero.Modules.Library.Application.AccessCodes.GenerateBatch;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Library.UnitTests.Application.AccessCodes;

public class GenerateBatchCommandHandlerTests
{
    private readonly IAccessCodeRepository _accessCodeRepository;
    private readonly ILibraryRepository _libraryRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<GenerateBatchCommandHandler> _logger;
    private readonly GenerateBatchCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid LibraryId = Guid.NewGuid();

    public GenerateBatchCommandHandlerTests()
    {
        _accessCodeRepository = Substitute.For<IAccessCodeRepository>();
        _libraryRepository = Substitute.For<ILibraryRepository>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _logger = Substitute.For<ILogger<GenerateBatchCommandHandler>>();
        
        _handler = new GenerateBatchCommandHandler(
            _accessCodeRepository,
            _libraryRepository,
            _tenantProvider,
            _passwordHasher,
            _logger);
    }

    [Fact]
    public async Task Handle_Should_GenerateCodes_WhenLibraryIsAuthorized()
    {
        // Arrange
        var resourceArn = "az:courses:tenant:course/101";
        var library = AlphaZero.Modules.Library.Domain.Library.Create("L1", "A1", "123", TenantId);
        library.AuthorizeResource(ResourceArn.Create(resourceArn).Value);

        _tenantProvider.GetTenant().Returns(TenantId);
        _libraryRepository.GetById(LibraryId).Returns(library);
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");

        var command = new GenerateBatchCommand(LibraryId, 5, "enroll-course", resourceArn, new());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(5);
        _accessCodeRepository.Received(5).Add(Arg.Any<AccessCode>());
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenLibraryNotAuthorizedForResource()
    {
        // Arrange
        var resourceArn = "az:courses:tenant:course/forbidden";
        var library = AlphaZero.Modules.Library.Domain.Library.Create("L1", "A1", "123", TenantId);

        _tenantProvider.GetTenant().Returns(TenantId);
        _libraryRepository.GetById(LibraryId).Returns(library);

        var command = new GenerateBatchCommand(LibraryId, 5, "enroll-course", resourceArn, new());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Library.Batch.Forbidden");
    }
}