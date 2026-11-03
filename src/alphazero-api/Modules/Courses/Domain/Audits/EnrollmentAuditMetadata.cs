using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;

namespace AlphaZero.Modules.Courses.Domain.Audits;

/// <summary>
/// Builder for the <see cref="EnrollmentAuditEntry"/> jsonb metadata (issue #34 §3.1): the
/// transition's source and target status plus, where the event carries one, the human cause.
/// </summary>
public static class EnrollmentAuditMetadata
{
    public const string FromKey = "From";
    public const string ToKey = "To";
    public const string CauseKey = "cause";

    public static Dictionary<string, string> FromTransition(
        EnrollmentStatus from, EnrollmentStatus to, string? cause = null)
    {
        var metadata = new Dictionary<string, string>
        {
            [FromKey] = from.ToString(),
            [ToKey] = to.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(cause))
        {
            metadata[CauseKey] = cause;
        }

        return metadata;
    }
}
