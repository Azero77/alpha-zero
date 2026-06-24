namespace AlphaZero.Modules.Courses.IntegrationEvents;

public record ItemCompletedIntegrationEvent(Guid EnrollmentId, Guid CourseId, int BitIndex, double OldCompletionPercentage, double NewCompletionPercentage);
