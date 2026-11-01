using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Application.Enrollments;

/// <summary>
/// Shared bookkeeping for every lifecycle transition handler (code-review smell #3): the
/// capture-previous-status → guarded transition → mark-the-aggregate rhythm lived tripled in
/// the Suspend / Reactivate / saga-Deactivate handlers. It lives here now.
///
/// The mark matters: <see cref="IRepository{TEntity}.Update"/> sets state to Modified even for an
/// unchanged tracked aggregate — for an idempotent repeat (guard resolved with no status change)
/// the helper skips the mark, so EF issues no UPDATE. The per-handler LOG stays with its
/// handler: each verb has its own payload, message and log level.
/// </summary>

[Obsolete] //The logic is now in the transition service
public static class EnrollmentTransitionBookkeeping
{
    /// <summary>
    /// Runs the guarded aggregate transition, then marks the loaded aggregate for persistence
    /// only when the guard genuinely transitioned it.
    /// </summary>
    /// <param name="repository">The enrollment repository the aggregate was loaded from.</param>
    /// <param name="enrollment">The loaded (change-tracked) aggregate.</param>
    /// <param name="guardedTransition">Closure calling the aggregate's guarded transition method.</param>
    /// <returns>
    /// The transition's result, plus whether the aggregate actually changed state — handlers use
    /// that to gate verb-specific logging (idempotent repeats log nothing/skipping).
    /// </returns>
    public static (ErrorOr<Success> Result, bool Changed) Transitioned(
        this IRepository<Enrollment> repository,
        Enrollment enrollment,
        Func<ErrorOr<Success>> guardedTransition)
    {
        var previousStatus = enrollment.Status;
        var result = guardedTransition();

        if (result.IsError)
            return (result, Changed: false);

        var changed = previousStatus != enrollment.Status;
        if (changed)
        {
            repository.Update(enrollment);
        }

        return (Result.Success, changed);
    }
}
