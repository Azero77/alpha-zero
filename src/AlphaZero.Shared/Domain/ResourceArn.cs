using AlphaZero.Shared.Authorization;
using ErrorOr;
using Microsoft.AspNetCore.Components.Forms;
using System.Text.RegularExpressions;

namespace AlphaZero.Shared.Domain;

/// <summary>
/// Represents a specific, fully-qualified Resource Name.
/// Must NOT contain wildcards.
/// Format: az:{service}:{tenantId}:{resourcePath}
/// </summary>
public record ResourceArn
{
    public string Service { get; }
    public string TenantIdString { get; }
    public string ResourcePath { get; }

    private const string ResourcePatternRegex = @"^az:(?<service>[a-zA-Z]+):(?<tenantId>[a-zA-Z0-9-]+):(?<resourcePath>[A-Za-z0-9\/\-]+)$";

    public ResourceArn(string service, string tenantId, string resourcePath)
    {
        Service = service.ToLowerInvariant();
        TenantIdString = tenantId.ToLowerInvariant();
        ResourcePath = resourcePath;
    }

    public static ErrorOr<ResourceArn> Create(string service, string tenantId, string resourcePath)
    {
        if (resourcePath.Contains("*"))
            return Error.Validation("Identity.Application","ResourceArn cannot contain wildcards. Use ResourcePattern for scopes");

        if (Enum.TryParse<ResourceType>(service, out _))
        {
            return Error.Validation("Identity.Application","Invalid service");
        }
        return new ResourceArn(service, tenantId, resourcePath);
    }
    public static ErrorOr<ResourceArn> Create(string value)
    {
        var match = Regex.Match(value, ResourcePatternRegex);

        if (!match.Success)
            return Error.Validation("Identity.Application" , "Invalid Resource Arn pattern");

        var service = match.Groups["service"].Value;
        var tenantId = match.Groups["tenantId"].Value;
        var path = match.Groups["resourcePath"].Value;
        return Create(service,tenantId,path);
    }
    public override string ToString() => $"az:{Service}:{TenantIdString}:{ResourcePath}";
}

/// <summary>
/// Represents a permission scope pattern.
/// CAN contain wildcards at the end.
/// Format: az:{service}:{tenantId}:{resourcePathPattern}
/// </summary>
public record ResourcePattern
{
    public string Value { get; }

    private ResourcePattern(string value)
    {
        if (!IsValid(value))
            throw new ArgumentException($"Invalid ResourcePattern format: {value}");
        
        Value = value.ToLowerInvariant();
    }

    public static ErrorOr<ResourcePattern> Create(string value)
    {

        if (!IsValid(value))
            Error.Validation("Identity.Application",$"Invalid ResourcePattern format: {value}");

        return new ResourcePattern(value);

    }

    public static bool IsValid(string pattern)
    {
        // Matches az:service:tenant:path or az:service:tenant:path/*
        return Regex.IsMatch(pattern, @"^az:[a-zA-Z\*]+:[a-zA-Z0-9-\*]+:[A-Za-z0-9\/\-\*]+$");
    }

    public bool IsMatch(ResourceArn arn)
    {
        var arnString = arn.ToString().ToLowerInvariant();
        
        // Simple Wildcard matching
        if (Value.EndsWith("*"))
        {
            var prefix = Value.TrimEnd('*');
            return arnString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return arnString == Value;
    }

    public static ResourcePattern All => new ResourcePattern("az:*");

    public override string ToString() => Value;
}
