namespace AlphaZero.Modules.Assessments.IntegrationEvents;

/// <summary>
/// Fact: Assessment metadata has changed. 
/// Used by Courses module to sync its materialized view (Read Model).
/// </summary>
public record AssessmentMetadataChangedIntegrationEvent(
    Guid AssessmentId,
    string Title,
    string Type, // "Practice", "Final", etc.
    decimal PassingScore,
    string Status);
