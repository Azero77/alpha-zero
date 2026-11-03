using AlphaZero.Modules.Courses.Application.Enrollments.Commands.MarkExpiredFromPrincipal;
using AlphaZero.Modules.Identity.IntegrationEvents;
using AlphaZero.Shared.Application;
using MassTransit;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

/// <summary>
/// Consumes Identity's <see cref="PrincipalExpiredIntegrationEvent"/> — the lazy expiry path (a)
/// of issue #34 rework §3.4: an AccessTicketValid evaluation saw an elapsed access window, and
/// Courses must idempotently mark the matching enrollment Expired via the aggregate's guarded
/// <c>MarkExpired</c>.
///
/// House conventions (mirrors MetadataChangedConsumerBase): the cross-module event is translated
/// into an APPLICATION COMMAND and dispatched through the module's fresh request scope, where the
/// transactional pipeline + UnitOfWork publish the domain events → audit row (§3.1) and the 1:1
/// <c>EnrollmentExpired</c> integration event. The <c>TenantConsumeFilter</c> has already restored
/// the ambient tenant from the event's <c>TenantId</c> before this consumer runs, so every query
/// inside the dispatch is tenant-scoped — a foreign-tenant event simply resolves to nothing.
///
/// Storm-safety (§2 R2-Q5): duplicate evaluations that arrive while marking is pending no-op —
/// the aggregate guard returns success without state churn; the mark runs exactly once. Known
/// deterministic no-work shapes (unresolvable enrollment, terminal Inactive) ACK cleanly; only
/// unexpected failures redeliver.
/// </summary>
public class PrincipalExpiredIntegrationEventConsumer(
    ICoursesModule coursesModule,
    ILogger<PrincipalExpiredIntegrationEventConsumer> logger) : IConsumer<PrincipalExpiredIntegrationEvent>
{
    /// <summary>
    /// Every course-scoped resource path begins <c>course/{id}</c> (course, section, lesson,
    /// assessment, coursevideo ARNs alike — see ResourceArn.For*).
    /// </summary>
    private static readonly Regex CourseInPath = new(
        @"^course/(?<id>[0-9A-Fa-f-]{36})(/.*)?$", RegexOptions.Compiled);

    public async Task Consume(ConsumeContext<PrincipalExpiredIntegrationEvent> context)
    {
        var message = context.Message;

        var courseMatch = CourseInPath.Match(message.ResourcePath);
        if (!courseMatch.Success)
        {
            logger.LogWarning(
                "PrincipalExpired for User {UserId} carries a non-course resource path '{ResourcePath}' — nothing at Courses to expire.",
                message.UserId, message.ResourcePath);
            return; // ACK: the operator is course-agnostic; non-course policies send no work here
        }

        var command = new MarkEnrollmentExpiredFromPrincipalCommand(
            UserId: message.UserId,
            CourseId: Guid.Parse(courseMatch.Groups["id"].Value));
        var result = await coursesModule.Send(command, context.CancellationToken);

        if (result.IsError)
        {
            // Deterministic no-work outcomes: the enrollment was never visible in this tenant
            // (stale ticket, retired enrollment, cross-tenant duplicate) or cannot legally
            // expire (terminal Inactive). Both absorb duplicate evaluations — ACK without retry.
            if (result.Errors.All(e => e.Code is "Enrollment.NotFound" or "Enrollment.InvalidTransition"))
            {
                logger.LogInformation(
                    "PrincipalExpired marked nothing: User {UserId}/Course {CourseId} resolved to a no-work state ({Codes}) — duplicate evaluations absorbed.",
                    message.UserId, command.CourseId, string.Join(',', result.Errors.Select(e => e.Code)));
                return;
            }

            logger.LogError(
                "PrincipalExpired could not mark User {UserId}/Course {CourseId} expired: {Errors} — redelivering.",
                message.UserId, command.CourseId, string.Join(", ", result.Errors.Select(e => e.Description)));

            throw new InvalidOperationException(
                $"MarkEnrollmentExpiredFromPrincipal failed for user {message.UserId} course {command.CourseId}.");
        }
    }
}
