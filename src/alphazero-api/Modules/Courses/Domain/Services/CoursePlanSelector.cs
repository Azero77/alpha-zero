using System.Text.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Services;

/// <summary>
/// Selects the course plan an access code activates (issue #58, multi-plan support).
/// The plan reference is expressed by the code's provisioning metadata under the
/// <c>"plan"</c> key (e.g. <c>{ "plan": "&lt;planId&gt;" }</c>), with a legacy
/// <c>/plan/{id}</c> ARN segment honoured at lower precedence for backwards
/// compatibility. Selection is strict: an explicit reference that does not resolve
/// is an error (never a fall-through to another plan), and a code with no reference
/// on a multi-plan course is ambiguous (never a name-based guess).
/// </summary>
public sealed class CoursePlanSelector
{
    /// <summary>Metadata key carrying the plan reference (resource-type provisioning contract).</summary>
    public const string PlanMetadataKey = "plan";

    public ErrorOr<CoursePlan> SelectPlan(Course course, JsonDocument? metadata, ResourceArn? arn)
    {
        if (TryGetExplicitPlanId(metadata, arn, out var planId, out var malformedReason))
        {
            if (malformedReason is not null)
            {
                return Error.Failure("CoursePlan.NotFound", malformedReason);
            }

            return course.Plans.FirstOrDefault(p => p.Id == planId) is { } selected
                ? selected
                : Error.Failure(
                    "CoursePlan.NotFound",
                    $"Plan '{planId}' was not found on course '{course.Id}'.");
        }

        if (course.Plans.Count == 1)
        {
            return course.Plans.Single();
        }

        if (course.Plans.Count == 0)
        {
            return Error.Failure(
                "CoursePlan.NotFound",
                $"No plan could be resolved for course '{course.Id}' because it has no plans.");
        }

        return Error.Failure(
            "CoursePlan.Ambiguous",
            $"Course '{course.Id}' has {course.Plans.Count} plans but the access code does not specify one. " +
            $"Regenerate the batch with metadata {{ \"{PlanMetadataKey}\": \"<planId>\" }} to activate a specific plan.");
    }

    private static bool TryGetExplicitPlanId(
        JsonDocument? metadata,
        ResourceArn? arn,
        out Guid planId,
        out string? malformedReason)
    {
        planId = Guid.Empty;
        malformedReason = null;

        // 1) Preferred contract: metadata { "plan": "<guid>" } (also accepts "planId" alias).
        if (metadata?.RootElement.ValueKind == JsonValueKind.Object)
        {
            var root = metadata.RootElement;
            if (root.TryGetProperty(PlanMetadataKey, out var element)
                || root.TryGetProperty("planId", out element))
            {
                var raw = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
                if (Guid.TryParse(raw, out planId))
                {
                    return true;
                }

                malformedReason =
                    $"Access code metadata \"{PlanMetadataKey}\" reference '{raw}' is not a valid plan id.";
                return true;
            }
        }

        // 2) Legacy contract: /plan/{id} ARN segment (e.g. az:course:..:course/{id}/plan/{planId}).
        // NOTE: ResourceArn.GetResource has no "plan" mapping (it throws), so parse the
        // segment directly here.
        if (arn != null && arn.ResourcePath.Contains("/plan/"))
        {
            var segments = arn.ResourcePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string? raw = null;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i].Equals("plan", StringComparison.OrdinalIgnoreCase))
                {
                    raw = segments[i + 1];
                    break;
                }
            }

            if (raw is not null && Guid.TryParse(raw, out planId))
            {
                return true;
            }

            malformedReason = $"Target resource ARN '{arn}' plan segment '{raw}' is not a valid plan id.";
            return true;
        }

        return false;
    }
}
