using System.Text.RegularExpressions;
using ErrorOr;

namespace AlphaZero.Modules.Tenants.Domain;

public record TenantBranding
{
    public const string DefaultPrimaryColor = "#2563eb";
    private static readonly Regex HexColorRegex = new(@"^#([0-9A-Fa-f]{6})$", RegexOptions.Compiled);

    public string PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public string? LogoUrl { get; init; }
    public string? DarkModeLogoUrl { get; init; }
    public string? FaviconUrl { get; init; }

    // Parameterless constructor for EF Core deserialization
    private TenantBranding()
    {
        PrimaryColor = DefaultPrimaryColor;
    }

    public TenantBranding(
        string primaryColor,
        string? secondaryColor = null,
        string? logoUrl = null,
        string? darkModeLogoUrl = null,
        string? faviconUrl = null)
    {
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        LogoUrl = logoUrl;
        DarkModeLogoUrl = darkModeLogoUrl;
        FaviconUrl = faviconUrl;
    }

    public static TenantBranding Default => new(DefaultPrimaryColor);

    public static ErrorOr<TenantBranding> Create(
        string? primaryColor = null,
        string? secondaryColor = null,
        string? logoUrl = null,
        string? darkModeLogoUrl = null,
        string? faviconUrl = null)
    {
        var resolvedPrimary = string.IsNullOrWhiteSpace(primaryColor) ? DefaultPrimaryColor : primaryColor.Trim();
        if (!HexColorRegex.IsMatch(resolvedPrimary))
        {
            return Error.Validation("Branding.InvalidHexFormat", "Primary color must be a valid 6-character hex color (e.g. #2563eb).");
        }

        string? resolvedSecondary = null;
        if (!string.IsNullOrWhiteSpace(secondaryColor))
        {
            var trimmedSecondary = secondaryColor.Trim();
            if (!HexColorRegex.IsMatch(trimmedSecondary))
            {
                return Error.Validation("Branding.InvalidHexFormat", "Secondary color must be a valid 6-character hex color (e.g. #2563eb).");
            }
            resolvedSecondary = trimmedSecondary;
        }

        return new TenantBranding(
            resolvedPrimary,
            resolvedSecondary,
            string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim(),
            string.IsNullOrWhiteSpace(darkModeLogoUrl) ? null : darkModeLogoUrl.Trim(),
            string.IsNullOrWhiteSpace(faviconUrl) ? null : faviconUrl.Trim());
    }
}
