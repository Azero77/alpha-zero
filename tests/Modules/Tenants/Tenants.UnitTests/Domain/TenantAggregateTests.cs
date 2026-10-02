using AlphaZero.Modules.Tenants.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Tenants.UnitTests.Domain;

public class TenantAggregateTests
{
    [Fact]
    public void Create_WithoutBranding_ShouldDefaultToPrecisionBlueAndEmitDomainEvent()
    {
        // Act
        var tenant = Tenant.Create("Alpha Academy", "alpha");

        // Assert
        tenant.Name.Should().Be("Alpha Academy");
        tenant.Subdomain.Should().Be("alpha");
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.Branding.Should().NotBeNull();
        tenant.Branding.PrimaryColor.Should().Be("#2563eb");

        var domainEvents = tenant.PopDomainEvents().ToList();
        domainEvents.Should().ContainSingle(e => e is TenantCreatedDomainEvent);
        var createdEvent = (TenantCreatedDomainEvent)domainEvents.First();
        createdEvent.TenantId.Should().Be(tenant.Id);
        createdEvent.Name.Should().Be("Alpha Academy");
        createdEvent.Subdomain.Should().Be("alpha");
    }

    [Fact]
    public void Create_WithCustomBranding_ShouldStoreBranding()
    {
        // Arrange
        var branding = TenantBranding.Create("#1E3A8A", "#F59E0B", "https://example.com/logo.svg").Value;

        // Act
        var tenant = Tenant.Create("Harvard Prep", "harvard", branding);

        // Assert
        tenant.Branding.PrimaryColor.Should().Be("#1E3A8A");
        tenant.Branding.SecondaryColor.Should().Be("#F59E0B");
        tenant.Branding.LogoUrl.Should().Be("https://example.com/logo.svg");
    }

    [Fact]
    public void UpdateBranding_ShouldUpdateValueAndEmitTenantBrandingUpdatedDomainEvent()
    {
        // Arrange
        var tenant = Tenant.Create("MIT Online", "mit");
        tenant.PopDomainEvents(); // Clear creation event

        var newBranding = TenantBranding.Create("#059669", "#F97316").Value;

        // Act
        var result = tenant.UpdateBranding(newBranding);

        // Assert
        result.IsError.Should().BeFalse();
        tenant.Branding.PrimaryColor.Should().Be("#059669");
        tenant.Branding.SecondaryColor.Should().Be("#F97316");

        var domainEvents = tenant.PopDomainEvents().ToList();
        domainEvents.Should().ContainSingle(e => e is TenantBrandingUpdatedDomainEvent);
        var brandingEvent = (TenantBrandingUpdatedDomainEvent)domainEvents.First();
        brandingEvent.TenantId.Should().Be(tenant.Id);
        brandingEvent.Subdomain.Should().Be("mit");
        brandingEvent.Branding.PrimaryColor.Should().Be("#059669");
    }
}
