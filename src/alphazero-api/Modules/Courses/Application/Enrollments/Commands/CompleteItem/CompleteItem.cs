using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
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
        var outcome = await LoadForCompletionAsync(
            _enrollmentRepository.GetByIdAsync, request.EnrollmentId, request.BitIndex, cancellationToken);
        if (outcome.IsError) return outcome.Errors;

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
        // Re-apply the bit against the authoritative row:
        //   - a concurrent writer that already completed the same item → MarkAsComplete is a
        //     no-op and the CompletedOn guard suppresses the duplicate milestone event;
        //   - a concurrent writer that completed a DIFFERENT final bit → its bit is in the
        //     fresh state, ours is re-applied on top — no lost bit, and the completion
        //     event fires exactly once against the merged mask.
        var outcome = await LoadForCompletionAsync(
            _enrollmentRepository.GetFreshByIdAsync, enrollmentId, bitIndex, cancellationToken);
        if (outcome.IsError) return outcome.Errors;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Item at bit index {BitIndex} completed for Enrollment {EnrollmentId} on the concurrency retry.", bitIndex, enrollmentId);
        return Result.Success;
    }

    // Shared load-apply seam for both completion flows. The loader selector keeps each call
    // site's intent explicit (Handle: plain tracked load; concurrency retry: fresh re-read
    // per D3) so the distinction never hides; the guards and the apply live here so the
    // two flows cannot drift apart.
    private async Task<ErrorOr<(Enrollment Enrollment, Course Course)>> LoadForCompletionAsync(
        Func<Guid, CancellationToken, Task<Enrollment?>> loadEnrollment,
        Guid enrollmentId,
        int bitIndex,
        CancellationToken cancellationToken)
    {
        // domain events raised on the apply are published in-process by UnitOfWork before the save; the integration events they translate ride the module's EntityFramework outbox and reach other modules only after the transaction commits.
        var enrollment = await loadEnrollment(enrollmentId, cancellationToken);
        if (enrollment is null) return Error.NotFound("Enrollment.NotFound", "Enrollment not found.");

        // The Course owns the active-state of curriculum positions: its ActiveItemsBitmask is the
        // validation range (which positions exist), the activity gate (only active items can be
        // completed) and the denominator of the completion percentage. It is passed as a defensive
        // copy — the enrollment never keeps a reference to the course's mask.
        var course = await _courseRepository.GetById(enrollment.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var result = enrollment.CompleteItem(bitIndex, course.SnapshotActiveItemsBitmask());
        if (result.IsError) return result.Errors;

        return (enrollment, course);
    }
}
