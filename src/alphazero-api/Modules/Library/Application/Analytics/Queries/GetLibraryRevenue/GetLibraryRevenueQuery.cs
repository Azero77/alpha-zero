using AlphaZero.Modules.Library.Application.Analytics;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Library.Application.Analytics.Queries.GetLibraryRevenue;

public record GetLibraryRevenueQuery(Guid? LibraryId) : IRequest<ErrorOr<LibraryRevenueDto>>;

public class GetLibraryRevenueQueryValidator : AbstractValidator<GetLibraryRevenueQuery>
{
    public GetLibraryRevenueQueryValidator()
    {
        RuleFor(x => x.LibraryId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("LibraryId must be non-empty when provided.");
    }
}

/// <summary>
/// Revenue at face value, per period (month), derived exclusively from SaleRecords (issue #59,
/// invariant I-MONEY). <c>LibraryId</c> null = tenant-wide read; the <c>Libraries</c> rollup
/// includes a null-LibraryId entry that aggregates money from codes without an owning library
/// (admin codes) — they are included in tenant totals but are not a partner library.
/// </summary>
public record LibraryRevenueDto(
    Guid? LibraryId,
    decimal TotalRevenue,
    int TotalSales,
    List<LibraryRevenuePeriodDto> Periods,
    List<LibraryRevenueLibraryDto> Libraries);

public record LibraryRevenuePeriodDto(
    string Period,
    int SalesCount,
    List<LibraryRevenueCurrencyDto> ByCurrency);

public record LibraryRevenueCurrencyDto(string Currency, decimal Revenue);

public record LibraryRevenueLibraryDto(
    Guid? LibraryId,
    decimal TotalRevenue,
    int TotalSales);

public sealed class GetLibraryRevenueQueryHandler(ILibraryAnalyticsQueryService analyticsQueryService)
    : IRequestHandler<GetLibraryRevenueQuery, ErrorOr<LibraryRevenueDto>>
{
    public async Task<ErrorOr<LibraryRevenueDto>> Handle(GetLibraryRevenueQuery request, CancellationToken cancellationToken)
    {
        var result = await analyticsQueryService.GetLibraryRevenueAsync(request.LibraryId, cancellationToken);
        return result;
    }
}
