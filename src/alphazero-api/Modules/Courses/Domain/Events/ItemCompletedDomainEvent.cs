using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class ItemCompletedDomainEvent : DomainEvent
{
    public Guid EnrollmentId { get; }
    public Guid CourseId { get; }
    public int BitIndex { get; }
    public double OldCompletionPercentage { get; }
    public double NewCompletionPercentage { get; }

    public ItemCompletedDomainEvent(Guid enrollmentId, Guid courseId, int bitIndex, double oldCompletionPercentage, double newCompletionPercentage)
    {
        EnrollmentId = enrollmentId;
        CourseId = courseId;
        BitIndex = bitIndex;
        OldCompletionPercentage = oldCompletionPercentage;
        NewCompletionPercentage = newCompletionPercentage;
    }
}
