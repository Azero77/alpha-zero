using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.CompleteItem;

public record CompleteItemCommand(Guid EnrollmentId, int BitIndex) : ICommand<Success>, ITransactionalCommand;

public sealed class CompleteItemCommandHandler : IRequestHandler<CompleteItemCommand, ErrorOr<Success>>
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<CompleteItemCommandHandler> _logger;

    public CompleteItemCommandHandler(
        IEnrollmentRepository enrollmentRepository,
        ICourseRepository courseRepository,
        ILogger<CompleteItemCommandHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(CompleteItemCommand request, CancellationToken cancellationToken)
    {
        // FOR UPDATE: the row-level exclusive lock serialises concurrent completions on the
        // same enrollment at the database level — no optimistic-concurrency retry needed.
        // The lock is held for the duration of the ambient transaction (~milliseconds).
        var enrollment = await _enrollmentRepository.GetByIdForUpdateAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null)
            return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");

        // The Course owns the active-state of curriculum positions: its ActiveItemsBitmask is the
        // validation range (which positions exist), the activity gate (only active items can be
        // completed) and the denominator of the completion percentage. It is passed as a defensive
        // copy — the enrollment never keeps a reference to the course's mask.
        var course = await _courseRepository.GetById(enrollment.CourseId, cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var result = enrollment.CompleteItem(request.BitIndex, course.SnapshotActiveItemsBitmask());
        if (result.IsError) return result.Errors;

        // SaveChangesAsync is handled by the UnitOfWorkDecoratorCommandHandler pipeline behaviour.
        _logger.LogInformation("Item at bit index {BitIndex} completed for Enrollment {EnrollmentId}.",
            request.BitIndex, request.EnrollmentId);
        return Result.Success;
    }
}
