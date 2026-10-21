using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands.CompleteItem;

public record CompleteItemCommand(Guid EnrollmentId, int BitIndex) : ICommand<Success>;

public sealed class CompleteItemCommandHandler : IRequestHandler<CompleteItemCommand, ErrorOr<Success>>
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompleteItemCommandHandler> _logger;

    public CompleteItemCommandHandler(
        IEnrollmentRepository enrollmentRepository,
        ICourseRepository courseRepository,
        IUnitOfWork unitOfWork,
        ILogger<CompleteItemCommandHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _courseRepository = courseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(CompleteItemCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(request.EnrollmentId, cancellationToken);
        if (enrollment is null) return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");

        // The Course owns the active-state of curriculum positions: its ActiveItemsBitmask is the
        // validation range (which positions exist), the activity gate (only active items can be
        // completed) and the denominator of the completion percentage. It is passed as a defensive
        // copy — the enrollment never keeps a reference to the course's mask.
        var course = await _courseRepository.GetById(enrollment.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = enrollment.CompleteItem(request.BitIndex, course.SnapshotActiveItemsBitmask());
        if (result.IsError) return result.Errors;

        // The completion transition (#32) must fire exactly once per enrollment. Enrollment
        // carries an xmin rowversion token, so a completion that raced a concurrent writer
        // loses at SaveChanges with DbUpdateConcurrencyException instead of silently
        // overwriting the other writer's row; retry ONCE against fresh state and let the
        // aggregate's CompletedOn guard decide what remains to be raised (D3).
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "Completion of enrollment {EnrollmentId} (bit {BitIndex}) conflicted with a concurrent writer; retrying once against fresh state.",
                request.EnrollmentId, request.BitIndex);

            return await RetryCompletionOnceAsync(request.EnrollmentId, request.BitIndex, cancellationToken);
        }

        _logger.LogInformation("Item at bit index {BitIndex} completed for Enrollment {EnrollmentId}.", request.BitIndex, request.EnrollmentId);
        return Result.Success;
    }

    private async Task<ErrorOr<Success>> RetryCompletionOnceAsync(Guid enrollmentId, int bitIndex, CancellationToken cancellationToken)
    {
        var enrollment = await _enrollmentRepository.GetFreshByIdAsync(enrollmentId, cancellationToken);
        if (enrollment is null) return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");

        var course = await _courseRepository.GetById(enrollment.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        // Re-apply the bit against the authoritative row:
        //   - a concurrent writer that already completed the same item → MarkAsComplete is a
        //     no-op and the CompletedOn guard suppresses the duplicate milestone event;
        //   - a concurrent writer that completed a DIFFERENT final bit → its bit is in the
        //     fresh state, ours is re-applied on top — no lost bit, and the completion
        //     event fires exactly once against the merged mask.
        var result = enrollment.CompleteItem(bitIndex, course.SnapshotActiveItemsBitmask());
        if (result.IsError) return result.Errors;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Item at bit index {BitIndex} completed for Enrollment {EnrollmentId} on the concurrency retry.", bitIndex, enrollmentId);
        return Result.Success;
    }
}
