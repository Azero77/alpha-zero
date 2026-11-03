namespace AlphaZero.Modules.Identity.IntegrationEvents;

/// <summary>
/// Request to ensure that a per-(course, plan) role principal exists in Identity (issue #34 rework §3.2.1).
/// Handled asynchronously via MassTransit request-response over the in-memory module bus.
/// </summary>
public record EnsureCoursePlanPrincipalRequest(Guid CourseId, Guid PlanId, Guid TenantId);

/// <summary>
/// Success response returning the ensured principal ID.
/// </summary>
public record CoursePlanPrincipalEnsuredResponse(Guid PrincipalId);

/// <summary>
/// Failure response when principal creation fails.
/// </summary>
public record CoursePlanPrincipalEnsureFailedResponse(string Reason, string? Code = null);
