using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetBySubdomain;
using AlphaZero.Modules.Tenants.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Tenants.UnitTests.Application;

public class GetTenantBySubdomainQueryHandlerTests
{
    private readonly ITenantRepository _tenantRepository = Substitute.For<ITenantRepository>();
    private readonly GetTenantBySubdomainQueryHandler _handler;

    public GetTenantBySubdomainQueryHandlerTests()
    {
        _handler = new GetTenantBySubdomainQueryHandler(_tenantRepository);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        _tenantRepository.GetBySubdomainAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        var query = new GetTenantBySubdomainQuery("unknown");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public async Task Handle_WhenFound_ShouldMapFullBrandingToDto()
    {
        // Arrange
        var branding = TenantBranding.Create(
            primaryColor: "#059669",
            secondaryColor: "#F97316",
            logoUrl: "https://example.com/logo.png",
            darkModeLogoUrl: "https://example.com/dark.png",
            faviconUrl: "https://example.com/favicon.ico").Value;

        var tenant = Tenant.Create("Damascus Tech", "damascus", branding);

        _tenantRepository.GetBySubdomainAsync("damascus", Arg.Any<CancellationToken>())
            .Returns(tenant);

        var query = new GetTenantBySubdomainQuery("damascus");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        var dto = result.Value;
        dto.Id.Should().Be(tenant.Id);
        dto.Name.Should().Be("Damascus Tech");
        dto.Subdomain.Should().Be("damascus");
        dto.PrimaryColor.Should().Be("#059669");
        dto.SecondaryColor.Should().Be("#F97316");
        dto.LogoUrl.Should().Be("https://example.com/logo.png");
        dto.DarkModeLogoUrl.Should().Be("https://example.com/dark.png");
        dto.FaviconUrl.Should().Be("https://example.com/favicon.ico");
    }
}
