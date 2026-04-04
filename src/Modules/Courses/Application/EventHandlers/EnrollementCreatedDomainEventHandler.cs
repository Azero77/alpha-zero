using AlphaZero.Modules.Courses.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.EventHandlers;

public class EnrollementCreatedDomainEventHandler : INotificationHandler<EnrollementCreatedDomainEvent>
{
    private readonly ILogger<EnrollementCreatedDomainEventHandler> _logger;

    public EnrollementCreatedDomainEventHandler(ILogger<EnrollementCreatedDomainEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(EnrollementCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        // This is a placeholder for future logic (e.g. sending a welcome email, initializing student stats).
        _logger.LogInformation("Domain Event Handled: Enrollment {EnrollmentId} created for Student {StudentId} in Course {CourseId}.",
            notification.EnrollementId, notification.StudentId, notification.CourseId);
        return Task.CompletedTask;
    }
}
