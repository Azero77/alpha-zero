namespace AlphaZero.Shared.Domain;

/// <summary>
/// A value object representing a unique Resource Name (ARN/URN).
/// Format: az:{service}:{tenantId}:{resourcePath}
/// Example: az:courses:school-1:course/math-101/section/sec-1
/// </summary>
public record ResourceArn
{
    public string Service { get; }
    public string TenantIdString { get; } // Can be a GUID or "global"
    public string ResourcePath { get; }

    private const string Prefix = "az";
    public const string GlobalTenant = "global";

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
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        // 1. Exact Match
        if (pattern == this.ToString()) return true;

        // 2. Wildcard Match (Prefix)
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern.Substring(0, pattern.Length - 1);
            return this.ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public override string ToString() => $"{Prefix}:{Service}:{TenantIdString}:{ResourcePath}";

    public static ResourceArn Parse(string arn)
    {
        var parts = arn.Split(':');
        if (parts.Length < 4 || parts[0] != Prefix)
            throw new ArgumentException("Invalid ARN format. Expected az:service:tenantId:path", nameof(arn));

        return new ResourceArn(parts[1], parts[2], parts[3]);
    }

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
