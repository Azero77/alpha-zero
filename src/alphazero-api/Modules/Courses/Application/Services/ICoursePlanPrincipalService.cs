using ErrorOr;

namespace AlphaZero.Modules.Courses.Application.Services;

/// <summary>
/// Service abstraction for ensuring that a per-(course, plan) role principal exists in Identity.
/// Isolates the Courses application layer from cross-module transport (MassTransit request-response via IBus).
/// </summary>
public interface ICoursePlanPrincipalService
{
    Task<ErrorOr<Guid>> EnsurePlanPrincipalAsync(
        Guid courseId,
        Guid planId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
