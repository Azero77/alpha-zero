namespace AlphaZero.Modules.Assessments.IntegrationEvents;

/// <summary>
/// Command: Request to create a new assessment from another module.
/// </summary>
public record CreateAssessmentRequest(
    string Title,
    string Type,
    decimal PassingScore,
    string Description,
    Guid TenantId
    );
/// <summary>
/// Response: Success response with the new ID.
/// </summary>
public record AssessmentCreatedResponse(Guid AssessmentId);

/// <summary>
/// Response: Failure response if validation or creation fails.
/// </summary>
public record AssessmentCreationFailedResponse(string Reason);
