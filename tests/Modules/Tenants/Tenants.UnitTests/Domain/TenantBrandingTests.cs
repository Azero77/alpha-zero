using AlphaZero.Modules.Tenants.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Tenants.UnitTests.Domain;

public class TenantBrandingTests
{
    [Fact]
    public void Default_ShouldHavePrecisionBluePrimaryColor()
    {
        // Act
        var branding = TenantBranding.Default;

        // Assert
        branding.PrimaryColor.Should().Be("#2563eb");
        branding.SecondaryColor.Should().BeNull();
        branding.LogoUrl.Should().BeNull();
        branding.DarkModeLogoUrl.Should().BeNull();
        branding.FaviconUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenPrimaryColorIsNullOrEmpty_ShouldDefaultToPrecisionBlue(string? primaryColor)
    {
        // Act
        var result = TenantBranding.Create(primaryColor);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.PrimaryColor.Should().Be("#2563eb");
    }

    [Theory]
    [InlineData("#1E3A8A")]
    [InlineData("#ffffff")]
    [InlineData("#000000")]
    [InlineData("#abcdef")]
    [InlineData("#123456")]
    public void Create_WhenPrimaryColorIsValidHex_ShouldSucceed(string validHex)
    {
        // Act
        var result = TenantBranding.Create(validHex);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.PrimaryColor.Should().Be(validHex);
    }

    [Theory]
    [InlineData("1E3A8A")] // Missing #
    [InlineData("#12345")] // 5 chars
    [InlineData("#1234567")] // 7 chars
    [InlineData("#GGG000")] // Invalid hex characters
    [InlineData("red")] // Color name
    [InlineData("#12 345")] // Contains space
    public void Create_WhenPrimaryColorIsInvalidHex_ShouldReturnInvalidHexFormatError(string invalidHex)
    {
        // Act
        var result = TenantBranding.Create(invalidHex);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Branding.InvalidHexFormat");
    }

    [Fact]
    public void Create_WhenSecondaryColorIsValidHex_ShouldSucceed()
    {
        // Act
        var result = TenantBranding.Create("#2563eb", "#F59E0B");

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.PrimaryColor.Should().Be("#2563eb");
        result.Value.SecondaryColor.Should().Be("#F59E0B");
    }

    [Fact]
    public void Create_WhenSecondaryColorIsInvalidHex_ShouldReturnInvalidHexFormatError()
    {
        // Act
        var result = TenantBranding.Create("#2563eb", "not-a-hex");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Branding.InvalidHexFormat");
    }

    [Fact]
    public void Create_WithAllProperties_ShouldTrimAndSetProperly()
    {
        // Act
        var result = TenantBranding.Create(
            primaryColor: " #059669 ",
            secondaryColor: " #F97316 ",
            logoUrl: " https://example.com/logo.png ",
            darkModeLogoUrl: " https://example.com/dark-logo.png ",
            faviconUrl: " https://example.com/favicon.ico ");

        // Assert
        result.IsError.Should().BeFalse();
        var branding = result.Value;
        branding.PrimaryColor.Should().Be("#059669");
        branding.SecondaryColor.Should().Be("#F97316");
        branding.LogoUrl.Should().Be("https://example.com/logo.png");
        branding.DarkModeLogoUrl.Should().Be("https://example.com/dark-logo.png");
        branding.FaviconUrl.Should().Be("https://example.com/favicon.ico");
    }
}
