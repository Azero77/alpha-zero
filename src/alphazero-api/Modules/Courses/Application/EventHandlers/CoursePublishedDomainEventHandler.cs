using AlphaZero.Modules.Courses.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.EventHandlers;

public class CoursePublishedDomainEventHandler : INotificationHandler<CoursePublishedDomainEvent>
{
    private readonly ILogger<CoursePublishedDomainEventHandler> _logger;

    public CoursePublishedDomainEventHandler(ILogger<CoursePublishedDomainEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(CoursePublishedDomainEvent notification, CancellationToken cancellationToken)
    {
        // This is a placeholder for future logic (e.g. invalidating cache, updating search indexes, notifying enrolled students).
        _logger.LogInformation("Domain Event Handled: Course {CourseId} was published.", notification.CourseId);
        return Task.CompletedTask;
    }
}
