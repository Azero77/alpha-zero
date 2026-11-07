using AlphaZero.Modules.Library.Application.Analytics;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryFunnel;

public record GetLibraryFunnelQuery(Guid? LibraryId) : IRequest<ErrorOr<LibraryFunnelDto>>;

public class GetLibraryFunnelQueryValidator : AbstractValidator<GetLibraryFunnelQuery>
{
    public GetLibraryFunnelQueryValidator()
    {
        RuleFor(x => x.LibraryId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("LibraryId must be non-empty when provided.");
    }
}

/// <summary>
/// Inventory funnel totals (issue #59). <c>LibraryId</c> is null for tenant-wide reads; per-batch
/// detail lines are attached for the requested scope. Invariant I-MONEY: <c>Sold</c> counts derive
/// exclusively from SaleRecord rows, never from code status.
/// </summary>
public record LibraryFunnelDto(
    Guid? LibraryId,
    int TotalMinted,
    int Distributed,
    int Sold,
    int Redeemed,
    int Voided,
    int Expired,
    int Outstanding,
    int VoidedAfterSale,
    List<LibraryBatchFunnelDto> Batches);

/// <summary>
/// Funnel line per (library, batch). <c>LibraryId</c>/<c>BatchId</c> are Guid.Empty sentinels when
/// the code group has no library (admin codes) or was minted without a batch; sentinel-library rows
/// are excluded from tenant-wide batch lines (they roll into the tenant totals instead).
/// </summary>
public record LibraryBatchFunnelDto(
    Guid LibraryId,
    Guid BatchId,
    int Minted,
    int Distributed,
    int Sold,
    int Redeemed,
    int Voided,
    int Expired,
    int Outstanding,
    int VoidedAfterSale);

public sealed class GetLibraryFunnelQueryHandler(ILibraryAnalyticsQueryService analyticsQueryService)
    : IRequestHandler<GetLibraryFunnelQuery, ErrorOr<LibraryFunnelDto>>
{
    public async Task<ErrorOr<LibraryFunnelDto>> Handle(GetLibraryFunnelQuery request, CancellationToken cancellationToken)
    {
        var result = await analyticsQueryService.GetLibraryFunnelAsync(request.LibraryId, cancellationToken);
        return result;
    }
}
