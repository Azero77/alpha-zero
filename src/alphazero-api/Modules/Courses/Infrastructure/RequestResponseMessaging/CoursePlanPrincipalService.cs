using AlphaZero.Modules.Courses.Application.Services;
using AlphaZero.Modules.Identity.IntegrationEvents;
using ErrorOr;
using MassTransit;

namespace AlphaZero.Modules.Courses.Infrastructure.RequestResponseMessaging;

/// <summary>
/// Infrastructure adapter implementing <see cref="ICoursePlanPrincipalService"/> using MassTransit's
/// request-response pattern via <see cref="IBus"/>.
/// </summary>
public class CoursePlanPrincipalService : ICoursePlanPrincipalService
{
    private readonly IRequestClient<EnsureCoursePlanPrincipalRequest> _requestClient;

    // Single 1-param constructor: two equal-length constructors are ambiguous to Autofac's
    // (greedy) constructor selection and a DI resolve of AddPlanCommandHandler 500s (spec-34).
    public CoursePlanPrincipalService(IBus moduleBus)
    {
        _requestClient = moduleBus.CreateRequestClient<EnsureCoursePlanPrincipalRequest>();
    }

    public async Task<ErrorOr<Guid>> EnsurePlanPrincipalAsync(
        Guid courseId,
        Guid planId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var response = await _requestClient.GetResponse<CoursePlanPrincipalEnsuredResponse, CoursePlanPrincipalEnsureFailedResponse>(
            new EnsureCoursePlanPrincipalRequest(courseId, planId, tenantId),
            cancellationToken);

        if (response.Is<CoursePlanPrincipalEnsuredResponse>(out var success))
        {
            return success.Message.PrincipalId;
        }

        if (response.Is<CoursePlanPrincipalEnsureFailedResponse>(out var failure))
        {
            var code = failure.Message.Code ?? "CoursePlanPrincipal.Failed";
            return Error.Failure(code, failure.Message.Reason);
        }

        return Error.Failure("CoursePlanPrincipal.Unknown", "Unknown error while ensuring course plan principal.");
    }
}
