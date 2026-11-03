using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.EventHandlers;

/// <summary>
/// Keeps Course.ActiveItemsBitmask consistent with every curriculum modification. One focused
/// handler translates each local domain event into the matching Course mask operation, so the
/// curriculum change and the mask update are persisted by the SAME SaveChangesAsync transaction
/// (events are dispatched by the UnitOfWork before the save). Each mutating path re-commits the
/// aggregate with an explicit <see cref="ICourseRepository.Update"/> (spec-33 conversion rule:
/// handlers must not rely on implicit change-tracker write-through) so the write-side cache
/// decorator queues the course key set. This is a local domain invariant — deliberately NOT the
/// MassTransit outbox pipeline (see DomainEventHandler for integration events).
/// </summary>
public class CourseActiveItemsDomainEventHandler :
    INotificationHandler<CourseSectionAddedDomainEvent>,
    INotificationHandler<CourseSectionDeletedDomainEvent>,
    INotificationHandler<CurriculumItemAddedDomainEvent>,
    INotificationHandler<CurriculumItemDeletedDomainEvent>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<CourseActiveItemsDomainEventHandler> _logger;

    public CourseActiveItemsDomainEventHandler(
        ICourseRepository courseRepository,
        ILogger<CourseActiveItemsDomainEventHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public Task Handle(CourseSectionAddedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Sections are containers and carry no BitIndex: a section with no items changes nothing
        // on the mask. If items are added to it later, CurriculumItemAddedDomainEvent fires per item.
        _logger.LogDebug("Section {SectionId} added to course {CourseId}; no progress-tracked positions affected.",
            notification.SectionId, notification.CourseId);
        return Task.CompletedTask;
    }

    public async Task Handle(CourseSectionDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        var course = await LoadCourseOrFail(notification.CourseId, cancellationToken);
        course.DeactivateCurriculumBits(notification.BitIndexes);
        _courseRepository.Update(course);

        _logger.LogInformation("Section {SectionId} deleted in course {CourseId}; deactivated {BitCount} positions ({BitIndexes}).",
            notification.SectionId, notification.CourseId, notification.BitIndexes.Count, string.Join(',', notification.BitIndexes));
    }

    public async Task Handle(CurriculumItemAddedDomainEvent notification, CancellationToken cancellationToken)
    {
        var course = await LoadCourseOrFail(notification.CourseId, cancellationToken);
        course.ActivateCurriculumBit(notification.BitIndex);
        _courseRepository.Update(course);

        _logger.LogInformation("Curriculum item {ItemId} added to course {CourseId}; activated bit position {BitIndex}.",
            notification.ItemId, notification.CourseId, notification.BitIndex);
    }

    public async Task Handle(CurriculumItemDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        var course = await LoadCourseOrFail(notification.CourseId, cancellationToken);
        course.DeactivateCurriculumBits([notification.BitIndex]);
        _courseRepository.Update(course);

        _logger.LogInformation("Curriculum item {ItemId} deleted from course {CourseId}; deactivated bit position {BitIndex}.",
            notification.ItemId, notification.CourseId, notification.BitIndex);
    }

    /// <summary>
    /// The course is already tracked by the ambient AppDbContext (the command handler mutated it),
    /// so resolving it by id yields the same in-memory instance — the mask mutation lands on the
    /// aggregate the caller will persist. A missing course means the event refers to state that
    /// will never be persisted: fail loudly so the whole transaction rolls back.
    /// </summary>
    private async Task<Domain.Aggregates.Courses.Course> LoadCourseOrFail(Guid courseId, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetById(courseId, cancellationToken);
        if (course is null)
            throw new InvalidOperationException(
                $"Course {courseId} not found while applying a curriculum active-mask change. The transaction will roll back.");

        return course;
    }
}
