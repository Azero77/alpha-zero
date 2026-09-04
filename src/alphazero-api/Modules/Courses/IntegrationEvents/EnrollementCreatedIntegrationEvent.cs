namespace AlphaZero.Modules.Courses.IntegrationEvents;

public record EnrollementCreatedIntegrationEvent(Guid EnrollmentId, Guid StudentId, Guid CourseId);
