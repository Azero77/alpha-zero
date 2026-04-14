using AlphaZero.Shared.Authorization;
using ErrorOr;
using System.Text.RegularExpressions;

namespace AlphaZero.Shared.Domain;

/// <summary>
/// Represents a specific, fully-qualified Resource Name.
/// Must NOT contain wildcards.
/// Format: az:{service}:{tenantId}:{resourcePath}
/// </summary>
public class ResourceArn
{
    public string Service { get; private set; }
    public string TenantIdString { get; private set; }
    public string ResourcePath { get; private set; }

    public const string Prefix = "az";
    public const string GlobalTenant = "global";

    // Strict pattern for concrete ARNs
    private const string ConcreteRegex = @"^az:(?<service>[a-zA-Z]+):(?<tenantId>[a-zA-Z0-9-]+):(?<resourcePath>[A-Za-z0-9\/\-]+)$";

    private ResourceArn(string service, string tenantId, string resourcePath)
    {
        Service = service.ToLowerInvariant();
        TenantIdString = tenantId.ToLowerInvariant();
        ResourcePath = resourcePath;
    }

    private ResourceArn(string service, Guid tenantId, string resourcePath)
    {
        Service = service.ToLowerInvariant();
        TenantIdString = tenantId.ToString();
        ResourcePath = resourcePath;
    }

    public static ErrorOr<ResourceArn> Create(string service, string tenantId, string resourcePath)
    {
        if (resourcePath.Contains("*"))
            return Error.Validation("Identity.Application", "ResourceArn cannot contain wildcards. Use ResourcePattern for scopes");

        if (!Enum.TryParse<ResourceType>(service, ignoreCase: true, result: out _))
        {
            return Error.Validation("Identity.Application", "Invalid service");
        }
        return new ResourceArn(service, tenantId, resourcePath);
    }

    public static ErrorOr<ResourceArn> Create(string value)
    {
        var match = Regex.Match(value, ConcreteRegex);

        if (!match.Success)
            return Error.Validation("Identity.Application", "Invalid Resource Arn format. Expected az:{service}:{tenantId}:{path}");

        var service = match.Groups["service"].Value;
        var tenantId = match.Groups["tenantId"].Value;
        var path = match.Groups["resourcePath"].Value;
        return Create(service, tenantId, path);
    }

    public override string ToString() => $"{Prefix}:{Service}:{TenantIdString}:{ResourcePath}";

    // --- Static Factories ---

    public static ResourceArn ForTenant(Guid tenantId) =>
       new("tenants", GlobalTenant, $"tenant/{tenantId}");

    public static ResourceArn ForUser(Guid userId) =>
        new("identity", GlobalTenant, $"user/{userId}");

    public static ResourceArn ForPrincipal(Guid tenantId, Guid principalId) =>
        new("identity", tenantId, $"principal/{principalId}");

    public static ResourceArn ForPolicy(Guid tenantId, Guid policyId) =>
        new("identity", tenantId, $"policy/{policyId}");

    public static ResourceArn ForSubject(Guid tenantId, Guid subjectId) =>
        new("courses", tenantId, $"subject/{subjectId}");

    public static ResourceArn ForCourse(Guid tenantId, Guid courseId) =>
        new("courses", tenantId, $"course/{courseId}");

    public static ResourceArn ForSection(Guid tenantId, Guid courseId, Guid sectionId) =>
        new("courses", tenantId, $"course/{courseId}/section/{sectionId}");

    public static ResourceArn ForLesson(Guid tenantId, Guid courseId, Guid sectionId, Guid lessonId) =>
        new("courses", tenantId, $"course/{courseId}/section/{sectionId}/lesson/{lessonId}");

    public static ResourceArn ForQuiz(Guid tenantId, Guid courseId, Guid sectionId, Guid quizId) =>
        new("courses", tenantId, $"course/{courseId}/section/{sectionId}/quiz/{quizId}");

    public static ResourceArn ForEnrollment(Guid tenantId, Guid enrollmentId) =>
        new("courses", tenantId, $"enrollment/{enrollmentId}");

    public static ResourceArn ForVideo(Guid tenantId, Guid videoId) =>
        new("video", tenantId, $"video/{videoId}");

    public static ResourceArn ForLibrary(Guid tenantId, Guid libraryId) =>
        new("library", tenantId, $"library/{libraryId}");

    public static ResourceArn ForAccessCode(Guid tenantId, Guid codeId) =>
        new("library", tenantId, $"code/{codeId}");
}

/// <summary>
/// Represents a permission scope pattern.
/// CAN contain wildcards and placeholders.
/// Format: az:{service}:{tenantId}:{resourcePathPattern}
/// </summary>
public class ResourcePattern
{
    public string Value { get; private set; }

    private const string PatternRegex = @"^(?<prefix>az)(:(?<service>[a-zA-Z*]+))?(:(?<tenantId>[a-zA-Z0-9-*]+))?(:(?<resourcePath>[A-Za-z0-9/*/-{}]+))?$";


    private ResourcePattern(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static ErrorOr<ResourcePattern> Create(string value)
    {
        var result = IsValidPattern(value);
        if (result.IsError)
            return result.Errors;

        return new ResourcePattern(value);
    }

    public static ErrorOr<Match> IsValidPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return Error.Validation("Identity.Application", "Pattern cannot be empty.");

        var regex = new Regex(PatternRegex, RegexOptions.Compiled);
        var match = regex.Match(pattern);

        if (!match.Success)
            return Error.Validation("Identity.Application", "Invalid ARN pattern format.");

        var resourcePath = match.Groups["resourcePath"]?.Value;
        if (!string.IsNullOrEmpty(resourcePath))
        {
            if (resourcePath.Contains("*") && !resourcePath.EndsWith("*"))
                return Error.Validation("Identity.Application", "Invalid ARN pattern format: wildcard '*' is only allowed at the end of the resource path.");

            if (!IsValidPath(resourcePath))
                return Error.Validation("Identity.Application", "Invalid ARN pattern format: resource path contains invalid characters.");
        }

        return match;
    }

    public static bool IsValidPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment == "*") continue;
            if (segment.StartsWith("{") && segment.EndsWith("}")) continue;
            if (!Regex.IsMatch(segment, @"^[a-zA-Z0-9-]+$")) return false;
        }

        return true;
    }

    public bool IsMatch(ResourceArn arn)
    {
        var match = Regex.Match(Value, PatternRegex);
        if (!match.Success) return false;

        var pPrefix = match.Groups["prefix"].Value;
        var pService = match.Groups["service"].Value;
        var pTenantId = match.Groups["tenantId"].Value;
        var pResourcePath = match.Groups["resourcePath"].Value;

        // Prefix check (az)
        if (pPrefix != ResourceArn.Prefix) return false;

        // Service check
        if (pService != "*" && !string.Equals(pService, arn.Service, StringComparison.OrdinalIgnoreCase))
            return false;

        // Tenant check
        if (!string.IsNullOrEmpty(pTenantId) && pTenantId != "*" && !string.Equals(pTenantId, arn.TenantIdString, StringComparison.OrdinalIgnoreCase))
            return false;

        // Resource Path check
        if (string.IsNullOrEmpty(pResourcePath) || pResourcePath == "*")
            return true;

        if (pResourcePath.EndsWith("*"))
        {
            var prefix = pResourcePath.TrimEnd('*', '/');
            return arn.ResourcePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        // Handle placeholders in the pattern (e.g., az:courses:*:course/{courseId})
        // If no placeholders were actually used, simple comparison is faster.
        if (!pResourcePath.Contains("{"))
            return string.Equals(pResourcePath, arn.ResourcePath, StringComparison.OrdinalIgnoreCase);

        // Convert the pattern's path into a regex where {placeholder} matches any segment.
        var pathRegexPattern = "^" + Regex.Escape(pResourcePath)
            .Replace(@"\{", "{").Replace(@"\}", "}") // unescape brackets
            .Replace("{", "(?<").Replace("}", ">[^/]+)") // convert {id} to (?<id>[^/]+)
            + "$";

        try
        {
            return Regex.IsMatch(arn.ResourcePath, pathRegexPattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static ResourcePattern All => new ResourcePattern("az:*");

    public override string ToString() => Value;
}
