using AlphaZero.Modules.Tenants.Application.Tenants.Commands.CreateTenant;
using AlphaZero.Modules.Tenants.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Tenants.UnitTests.Application;

public class CreateTenantCommandHandlerTests
{
    private readonly ITenantRepository _tenantRepository = Substitute.For<ITenantRepository>();
    private readonly ILogger<CreateTenantCommandHandler> _logger = Substitute.For<ILogger<CreateTenantCommandHandler>>();
    private readonly CreateTenantCommandHandler _handler;

    public CreateTenantCommandHandlerTests()
    {
        _handler = new CreateTenantCommandHandler(_tenantRepository, _logger);
    }

    [Fact]
    public async Task Handle_WhenSubdomainNotUnique_ShouldReturnConflictError()
    {
        // Arrange
        _tenantRepository.IsSubdomainUniqueAsync("harvard", Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new CreateTenantCommand("Harvard Prep", "harvard");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Tenant.SubdomainNotUnique");
    }

    [Fact]
    public async Task Handle_WhenInvalidPrimaryColor_ShouldReturnValidationError()
    {
        // Arrange
        _tenantRepository.IsSubdomainUniqueAsync("harvard", Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new CreateTenantCommand(
            "Harvard Prep",
            "harvard",
            PrimaryColor: "invalid-color");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Branding.InvalidHexFormat");
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldPersistTenantWithBranding()
    {
        // Arrange
        _tenantRepository.IsSubdomainUniqueAsync("harvard", Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new CreateTenantCommand(
            "Harvard Online",
            "harvard",
            LogoUrl: "https://example.com/logo.png",
            PrimaryColor: "#1E3A8A",
            SecondaryColor: "#F59E0B");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();

        _tenantRepository.Received(1).Add(Arg.Is<Tenant>(t =>
            t.Name == "Harvard Online" &&
            t.Subdomain == "harvard" &&
            t.Branding.PrimaryColor == "#1E3A8A" &&
            t.Branding.SecondaryColor == "#F59E0B" &&
            t.Branding.LogoUrl == "https://example.com/logo.png"));
    }
}
