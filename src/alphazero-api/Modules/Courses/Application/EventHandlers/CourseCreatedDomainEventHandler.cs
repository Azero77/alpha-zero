using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Modules.Courses.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.EventHandlers;

public class CourseCreatedDomainEventHandler : INotificationHandler<CourseCreatedDomainEvent>
{
    private readonly ICourseOverviewRepository _courseOverviewRepository;
    private readonly ILogger<CourseCreatedDomainEventHandler> _logger;

    public CourseCreatedDomainEventHandler(
        ICourseOverviewRepository courseOverviewRepository,
        ILogger<CourseCreatedDomainEventHandler> logger)
    {
        _courseOverviewRepository = courseOverviewRepository;
        _logger = logger;
    }

    public Task Handle(CourseCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var defaultDoc = RichText.Create("{}").Value;
        var overview = CourseOverview.Create(
            Guid.NewGuid(),
            notification.TenantId,
            notification.CourseId,
            defaultDoc,
            null,
            null,
            null);

        _courseOverviewRepository.Add(overview);
        _logger.LogInformation("Domain Event Handled: Course {CourseId} auto-seeded initial CourseOverview.", notification.CourseId);

        return Task.CompletedTask;
    }
}
