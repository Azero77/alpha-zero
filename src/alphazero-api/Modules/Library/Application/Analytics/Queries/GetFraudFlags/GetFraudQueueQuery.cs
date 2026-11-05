using AlphaZero.Modules.Library.Application.Analytics;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Library.Application.Analytics.Queries.GetFraudFlags;

public record GetFraudQueueQuery(Guid? LibraryId) : IRequest<ErrorOr<List<FraudFlagDto>>>;

public class GetFraudQueueQueryValidator : AbstractValidator<GetFraudQueueQuery>
{
    public GetFraudQueueQueryValidator()
    {
        RuleFor(x => x.LibraryId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("LibraryId must be non-empty when provided.");
    }
}

/// <summary>
/// Per-request fraud queue computed over the append-only audit stream (issue #59). Rows link to
/// their audit entries via <see cref="LibraryAuditEntryRefDto"/>. <c>AccessCodeId</c> is
/// Guid.Empty for anomaly rows that are not attributable to a single code (velocity).
/// </summary>
public record FraudFlagDto(
    string Type,
    Guid AccessCodeId,
    Guid? LibraryId,
    DateTime OccurredAt,
    string Details,
    List<LibraryAuditEntryRefDto> RelatedAuditEntries);

/// <summary>Reference to an entry in the append-only LibraryAuditLog stream.</summary>
public record LibraryAuditEntryRefDto(
    Guid EventId,
    Guid AggregateId,
    string Action,
    Guid? ActorUserId,
    DateTime OccurredAt);

public sealed class GetFraudQueueQueryHandler(ILibraryAnalyticsQueryService analyticsQueryService)
    : IRequestHandler<GetFraudQueueQuery, ErrorOr<List<FraudFlagDto>>>
{
    public async Task<ErrorOr<List<FraudFlagDto>>> Handle(GetFraudQueueQuery request, CancellationToken cancellationToken)
    {
        var result = await analyticsQueryService.GetFraudFlagsAsync(request.LibraryId, cancellationToken);
        return result;
    }
}
