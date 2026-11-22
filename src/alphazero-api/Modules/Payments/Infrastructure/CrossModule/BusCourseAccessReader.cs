using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Payments.Application.Checkout;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Payments.Infrastructure.CrossModule;

/// <summary>Courses owns enrollment truth; Payments only consumes its narrow request-response contract.</summary>
public sealed class BusCourseAccessReader(IBus moduleBus, ILogger<BusCourseAccessReader> logger)
    : ICourseAccessReader
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public async Task<ErrorOr<bool>> HasActiveEnrollmentAsync(Guid tenantId, Guid userId,
        string targetResourceArn, CancellationToken cancellationToken)
    {
        try
        {
            var client = moduleBus.CreateRequestClient<CheckActiveCourseEnrollmentRequest>(RequestTimeout);
            var response = await client.GetResponse<CheckActiveCourseEnrollmentResponse>(
                new CheckActiveCourseEnrollmentRequest(tenantId, userId, targetResourceArn), cancellationToken);
            return response.Message.Succeeded
                ? response.Message.HasActiveEnrollment
                : Error.Conflict("Checkout.EnrollmentCheckUnavailable",
                    $"Courses could not evaluate enrollment eligibility ({response.Message.ErrorCode ?? "unknown"}).");
        }
        catch (Exception exception) when (exception is RequestTimeoutException or RequestFaultException)
        {
            logger.LogWarning(exception,
                "Courses enrollment eligibility lookup failed for tenant {TenantId}, user {UserId}.", tenantId, userId);
            return Error.Conflict("Checkout.EnrollmentCheckUnavailable",
                "Courses enrollment eligibility is unavailable; Checkout remains fail-closed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Courses enrollment eligibility lookup failed for tenant {TenantId}, user {UserId}.", tenantId, userId);
            return Error.Conflict("Checkout.EnrollmentCheckUnavailable",
                "Courses enrollment eligibility is unavailable; Checkout remains fail-closed.");
        }
    }
}
