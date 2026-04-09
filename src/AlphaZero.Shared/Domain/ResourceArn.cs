using Autofac.Core;
using ErrorOr;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AlphaZero.Shared.Domain;

/// <summary>
/// A value object representing a unique Resource Name (ARN/URN).
/// Format: az:{service}:{tenantId}:{resourcePath}
/// Example: az:courses:school-1:course/math-101/section/sec-1
/// For resource path , wild cards are only allowed at the end of the pattern and it means that all sub-resources are included in the permission, for example az:courses:school-1:course/math-101/* means math-101 course and all its sub-resources like sections and lessons.
/// placeholders are allowed in resouce path like {courseId} , but it is not evaluated as allowed when compared to actual values, it is usually used of principal scope
/// </summary>
/// 

public record ResourceArn
{
    public string Service { get; }
    public string TenantIdString { get; } // Can be a GUID or "global"
    public string ResourcePath { get; }

    private const string Prefix = "az";
    public const string GlobalTenant = "global";
    private const string ResourcePattern = @"^(?<prefix>az)(:(?<service>[a-zA-Z\*]+))?(:(?<tenantId>[a-zA-Z0-9-\*]+))?(:(?<resourcePath>[A-Za-z0-9\/\*\/\-\{\}]+))?$";

    public ResourceArn(string service, string tenantId, string resourcePath)
    {
        Service = service.ToLowerInvariant();
        TenantIdString = tenantId.ToLowerInvariant();
        ResourcePath = resourcePath;
    }

    public ResourceArn(string service, Guid tenantId, string resourcePath) 
        : this(service, tenantId.ToString(), resourcePath) { }

    /// <summary>
    /// Checks if this specific ARN is matched by a permission pattern (which may include wildcards).
    /// </summary>
    public bool IsMatchedBy(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        var parsed = Parse(pattern);
        if (parsed is null)
            return false;

        return MatchPrefix(parsed)
            && MatchService(parsed)
            && MatchTenant(parsed)
            && MatchResource(parsed);
    }

    public record PatternParts(string prefix, string service, string? tenantId, string? resourcePath)
    {
        public bool IsAllTenant => tenantId == "*" || string.IsNullOrEmpty(tenantId);
        public bool IsAllResources => resourcePath == "*" || string.IsNullOrEmpty(resourcePath);
        public bool IsAllServices => service == "*";
        public bool IsPrefixAllowed => prefix == ResourceArn.Prefix; //az
    }
    private bool MatchPrefix(PatternParts pattern)
    {
        return pattern.IsPrefixAllowed;
    }
    private bool MatchService(PatternParts pattern)
    {
        return pattern.IsAllServices || pattern.service == Service;
    }
    private bool MatchTenant(PatternParts pattern)
    {
        return pattern.IsAllTenant || pattern.tenantId == TenantIdString;
    }
    private bool MatchResource(PatternParts p)
    {

        if (p.IsAllResources)
            return true; // all resources

        if (p.resourcePath == "*")
            return true;

        if (p.resourcePath.EndsWith("*"))
        {
            var prefix = p.resourcePath.TrimEnd('*','/');
            return ResourcePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return p.resourcePath == ResourcePath;
    }


    public static bool IsValidPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            // allow wildcard
            if (segment == "*")
                continue;

            // allow parameter
            if (segment.StartsWith("{") && segment.EndsWith("}"))
                continue;

            // normal segment
            if (!Regex.IsMatch(segment, @"^[a-zA-Z0-9\-]+$"))
                return false;
        }

        return true;
    }
    public PatternParts? Parse(string pattern)
    {
        var result = IsValidPattern(pattern);
        if (result.IsError)
            return null;
        return ExtractValues(result.Value);
    }
    public static ErrorOr<Match> IsValidPattern(string pattern)
    {
        var regex = new Regex(ResourcePattern, RegexOptions.Compiled);
        var match = regex.Match(pattern);

        if (!match.Success) return Error.Validation("Identity.Application","Invalid ARN pattern format.");
        var resourcePath = match.Groups["resourcePath"]?.Value;
        if (resourcePath is not null)
        {
            // we need to check if resource path contain the wild card at the middle of the path which is not allowed, for example az:courses:tenantId:course/*/section/sec-1 is not valid but az:courses:tenantId:course/* is valid
            if(resourcePath.Contains("*") && !resourcePath.EndsWith("*"))
                return Error.Validation("Identity.Application","Invalid ARN pattern format: wildcard '*' is only allowed at the end of the resource path.");

            if(!IsValidPath(resourcePath))
                return Error.Validation("Identity.Application", "Invalid ARN pattern format: resource path contains invalid characters.");
        }
        return match;
    }
    private PatternParts ExtractValues(Match match)
    {
        var prefix = match.Groups["prefix"].Value;
        var tenantId = match.Groups["tenantId"].Value;
        var service = match.Groups["service"].Value;
        var resourcePath = match.Groups["resourcePath"].Value;
        return new (prefix, service, tenantId, resourcePath);

    }
    public override string ToString() => $"{Prefix}:{Service}:{TenantIdString}:{ResourcePath}";

    // --- Static Factories for Consistency (Matching the Template Table) ---

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
