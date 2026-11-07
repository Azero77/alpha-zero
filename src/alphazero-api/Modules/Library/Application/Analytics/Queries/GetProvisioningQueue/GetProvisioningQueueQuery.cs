using AlphaZero.Modules.Library.Application.Analytics;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Library.Application.Analytics.Queries.GetProvisioningQueue;

public record GetProvisioningQueueQuery(Guid? LibraryId) : IRequest<ErrorOr<List<FailedProvisioningDto>>>;

public class GetProvisioningQueueQueryValidator : AbstractValidator<GetProvisioningQueueQuery>
{
    public GetProvisioningQueueQueryValidator()
    {
        RuleFor(x => x.LibraryId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("LibraryId must be non-empty when provided.");
    }
}

/// <summary>
/// Failed/pending provisioning queue item (issue #59): an activated access code whose provisioning
/// in the Courses module failed or is parked. Library-side enrichment (owning library, redeem time)
/// is joined from the Library audit stream.
/// </summary>
public record FailedProvisioningDto(
    Guid AccessCodeId,
    Guid UserId,
    string TargetResourceArn,
    string Status,
    string? FailureReason,
    int RetryCount,
    DateTime CreatedAt,
    DateTime LastAttemptedAt,
    Guid? LibraryId,
    DateTime? RedeemedAt);

public sealed class GetProvisioningQueueQueryHandler(ILibraryAnalyticsQueryService analyticsQueryService)
    : IRequestHandler<GetProvisioningQueueQuery, ErrorOr<List<FailedProvisioningDto>>>
{
    public async Task<ErrorOr<List<FailedProvisioningDto>>> Handle(GetProvisioningQueueQuery request, CancellationToken cancellationToken)
    {
        var result = await analyticsQueryService.GetProvisioningQueueAsync(request.LibraryId, cancellationToken);
        return result;
    }
}
