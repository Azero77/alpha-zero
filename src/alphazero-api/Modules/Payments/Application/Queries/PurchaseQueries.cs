using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Queries;

public sealed record PurchaseSummary(
    Guid PurchaseId,
    Guid ProductId,
    string ItemTitle,
    decimal SettlementAmount,
    string SettlementCurrency,
    string Status,
    DateTime PaidAt);

public sealed record PurchaseDetail(
    Guid PurchaseId,
    Guid ProductId,
    Guid PriceId,
    Guid CheckoutSessionId,
    Guid ProviderAccountId,
    Guid ProviderTransactionId,
    string ItemTitle,
    decimal PresentmentAmount,
    string PresentmentCurrency,
    decimal SettlementAmount,
    string SettlementCurrency,
    decimal? FxRate,
    decimal ProviderFee,
    decimal CommissionShare,
    string ExternalPaymentId,
    string Status,
    DateTime PaidAt,
    DateTime? RefundedAt);

public sealed record PurchasePage(IReadOnlyList<PurchaseSummary> Items, int TotalCount, int Page, int PageSize);

public interface IPaymentsReadStore
{
    Task<PurchasePage> ListStudentPurchasesAsync(Guid tenantId, Guid userId, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<PurchaseDetail?> GetStudentPurchaseAsync(Guid tenantId, Guid userId, Guid purchaseId,
        CancellationToken cancellationToken);
}

public sealed record ListStudentPurchases(Guid TenantId, Guid UserId, int Page = 1, int PageSize = 25)
    : IQuery<PurchasePage>;

public sealed class ListStudentPurchasesHandler(IPaymentsReadStore readStore)
    : IRequestHandler<ListStudentPurchases, ErrorOr<PurchasePage>>
{
    public async Task<ErrorOr<PurchasePage>> Handle(ListStudentPurchases request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || request.UserId == Guid.Empty || request.Page < 1
            || request.PageSize is < 1 or > 100)
            return Error.Validation("Payments.QueryScope", "Tenant, user, and a page size from 1 to 100 are required.");
        return await readStore.ListStudentPurchasesAsync(request.TenantId, request.UserId, request.Page,
            request.PageSize, cancellationToken);
    }
}

public sealed record GetStudentPurchase(Guid TenantId, Guid UserId, Guid PurchaseId) : IQuery<PurchaseDetail>;

public sealed class GetStudentPurchaseHandler(IPaymentsReadStore readStore)
    : IRequestHandler<GetStudentPurchase, ErrorOr<PurchaseDetail>>
{
    public async Task<ErrorOr<PurchaseDetail>> Handle(GetStudentPurchase request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || request.UserId == Guid.Empty || request.PurchaseId == Guid.Empty)
            return Error.Validation("Payments.QueryScope", "Tenant, user, and Purchase identities are required.");
        var purchase = await readStore.GetStudentPurchaseAsync(request.TenantId, request.UserId,
            request.PurchaseId, cancellationToken);
        return purchase is null
            ? Error.NotFound("Purchase.NotFound", "Purchase was not found.")
            : purchase;
    }
}
