using ErrorOr;

namespace AlphaZero.Shared.Authorization;

/// <summary>
/// Provides methods for evaluating authorization policies and determining whether a principal has the required
/// permissions to access a specified resource.
/// </summary>
/// <remarks>This service acts as a central point for policy evaluation, delegating policy data retrieval to the
/// configured policy repository. It is typically used in scenarios where access control decisions must be enforced
/// based on dynamic policies associated with principals and resources.</remarks>
public interface IPolicyEvaluatorService
{
    Task<ErrorOr<Success>> Authorize(Guid prinicapId, Guid tenantId, string resourcePath, ResourceType resourceType, string requiredPermission);
}



public enum ResourceType
{
    Course,
    Subject,
    User,
    Video,
    Section,
    Lesson,
    Quiz
}