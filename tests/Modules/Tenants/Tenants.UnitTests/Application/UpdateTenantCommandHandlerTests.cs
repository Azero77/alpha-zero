using AlphaZero.Modules.Tenants.Application.Tenants.Commands.UpdateTenant;
using AlphaZero.Modules.Tenants.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Tenants.UnitTests.Application;

public class UpdateTenantCommandHandlerTests
{
    private readonly ITenantRepository _tenantRepository = Substitute.For<ITenantRepository>();
    private readonly ILogger<UpdateTenantCommandHandler> _logger = Substitute.For<ILogger<UpdateTenantCommandHandler>>();
    private readonly UpdateTenantCommandHandler _handler;

    public UpdateTenantCommandHandlerTests()
    {
        _handler = new UpdateTenantCommandHandler(_tenantRepository, _logger);
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantRepository.GetById(tenantId).Returns((Tenant?)null);

        var command = new UpdateTenantCommand(
            tenantId,
            "Updated Name",
            "#1E3A8A",
            null,
            null,
            null,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task Handle_WhenPrimaryColorIsInvalidHex_ShouldReturnValidationError()
    {
        // Arrange
        var tenant = Tenant.Create("Existing Academy", "existing");
        _tenantRepository.GetById(tenant.Id).Returns(tenant);

        var command = new UpdateTenantCommand(
            tenant.Id,
            "Updated Name",
            "invalid-hex-no-hash",
            null,
            null,
            null,
            null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Branding.InvalidHexFormat");
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldUpdateBrandingAndCallRepository()
    {
        // Arrange
        var tenant = Tenant.Create("Existing Academy", "existing");
        _tenantRepository.GetById(tenant.Id).Returns(tenant);

        var command = new UpdateTenantCommand(
            tenant.Id,
            "New Academy Name",
            "#059669",
            "#F97316",
            "https://cdn.example.com/logo.png",
            "https://cdn.example.com/dark-logo.png",
            "https://cdn.example.com/favicon.ico");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        tenant.Name.Should().Be("New Academy Name");
        tenant.Branding.PrimaryColor.Should().Be("#059669");
        tenant.Branding.SecondaryColor.Should().Be("#F97316");
        tenant.Branding.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
        tenant.Branding.DarkModeLogoUrl.Should().Be("https://cdn.example.com/dark-logo.png");
        tenant.Branding.FaviconUrl.Should().Be("https://cdn.example.com/favicon.ico");

        _tenantRepository.Received(1).Update(tenant);
    }
}
