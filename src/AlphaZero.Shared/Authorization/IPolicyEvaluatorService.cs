using ErrorOr;

namespace AlphaZero.Shared.Authorization;

/// <summary>
/// Provides methods for evaluating authorization policies and determining whether a principal has the required
/// permissions to access a specified resource.
/// </summary>
public interface IPolicyEvaluatorService
{
    Task<ErrorOr<Success>> Authorize(
        Guid id, 
        Guid tenantId, 
        string resourcePath, 
        ResourceType resourceType, 
        string requiredPermission,
        string authMethod,
        Guid? sessionId = null);
}

public enum ResourceType
{
    Courses,
    Subjects,
    Users,
    Videos,
    Sections,
    Lessons,
    Quizzes,
    Tenants,
    Identity,
    Video,
    Library,
    Assessments,
    Submissions
}
