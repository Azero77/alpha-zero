using AlphaZero.Shared.Authorization;

namespace AlphaZero.Shared.Domain;

public static class ResourceArnHelper
{

    private static string[] GetSegments(ResourceArn arn)
    {
        if (string.IsNullOrWhiteSpace(arn.ResourcePath))
            throw new InvalidOperationException("Resource path is empty.");

        return arn.ResourcePath
                  .Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static Guid ParseGuid(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new InvalidOperationException($"Invalid GUID: {value}");

        return guid;
    }

    private static Guid GetByKey(string[] segments, string key)
    {
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return ParseGuid(segments[i + 1]);
            }
        }

        throw new InvalidOperationException($"Key '{key}' not found in resource path.");
    }

    // -------------------------------
    // Strongly-typed extractors
    // -------------------------------

    public static Guid GetTenantId(this ResourceArn arn)
    {
        var segments = GetSegments(arn);

        if (segments.Length == 2 && segments[0].Equals("tenant", StringComparison.OrdinalIgnoreCase))
            return ParseGuid(segments[1]);

        // fallback: sometimes tenant is embedded
        return GetByKey(segments, "tenant");
    }

    public static Guid GetUserId(this ResourceArn arn)
    {
        var segments = GetSegments(arn);

        if (segments.Length == 2 && segments[0].Equals("user", StringComparison.OrdinalIgnoreCase))
            return ParseGuid(segments[1]);

        return GetByKey(segments, "user");
    }

    public static Guid GetPrincipalId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "principal");
    }

    public static Guid GetPolicyId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "policy");
    }

    public static Guid GetSubjectId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "subject");
    }

    public static Guid GetCourseId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "course");
    }

    public static Guid GetSectionId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "section");
    }

    public static Guid GetLessonId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "lesson");
    }

    public static Guid GetAssessmentId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "assessment");
    }

    public static Guid GetEnrollmentId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "enrollment");
    }

    public static Guid GetVideoId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "video");
    }

    public static Guid GetLibraryId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "library");
    }

    public static Guid GetAccessCodeId(this ResourceArn arn)
    {
        return GetByKey(GetSegments(arn), "code");
    }

    // -------------------------------
    // Generic dispatcher (optional)
    // -------------------------------

    public static Guid GetResource(this ResourceArn arn, ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Tenants => arn.GetTenantId(),
            ResourceType.Users => arn.GetUserId(),
            ResourceType.Subjects => arn.GetSubjectId(),
            ResourceType.Courses=> arn.GetCourseId(),
            ResourceType.Sections => arn.GetSectionId(),
            ResourceType.Lessons => arn.GetLessonId(),
            ResourceType.Assessments => arn.GetAssessmentId(),
            //ResourceType.Enrollment => arn.GetEnrollmentId(),
            ResourceType.Video => arn.GetVideoId(),
            ResourceType.Library => arn.GetLibraryId(),
            //ResourceType.AccessCode => arn.GetAccessCodeId(),
            _ => throw new NotSupportedException($"Unsupported resource type: {resourceType}")
        };
    }

    // -------------------------------
    // Optional utilities
    // -------------------------------

    public static bool IsType(this ResourceArn arn, string key)
    {
        var segments = GetSegments(arn);

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static (string Key, Guid Id)[] GetAllResources(this ResourceArn arn)
    {
        var segments = GetSegments(arn);
        var result = new System.Collections.Generic.List<(string, Guid)>();

        for (int i = 0; i < segments.Length - 1; i += 2)
        {
            var key = segments[i];
            var value = segments[i + 1];

            if (Guid.TryParse(value, out var guid))
            {
                result.Add((key, guid));
            }
        }

        return result.ToArray();
    }
}
