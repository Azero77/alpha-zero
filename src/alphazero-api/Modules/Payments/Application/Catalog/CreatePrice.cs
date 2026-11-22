using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Catalog;

public interface IPriceRepository
{
    Task<Price?> FindForArchiveAsync(Guid priceId, CancellationToken cancellationToken);
    Task<bool> ProductExistsAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken);
    Task<bool> HasActivePriceAsync(Guid productId, string currency, CancellationToken cancellationToken);
    Task<bool> HasActiveCheckoutAsync(Guid priceId, CancellationToken cancellationToken);
    Task<bool> HasPaidPurchaseAsync(Guid priceId, CancellationToken cancellationToken);
    Task AddAsync(Price price, CancellationToken cancellationToken);
    Task UpdateAsync(Price price, CancellationToken cancellationToken);
}

public sealed record CreatePrice(Guid TenantId, Guid ProductId, decimal Amount, string Currency, Guid CreatedByUserId)
    : ICommand<Price>, ITransactionalCommand;

public sealed class CreatePriceHandler(IPriceRepository prices)
    : IRequestHandler<CreatePrice, ErrorOr<Price>>
{
    public async Task<ErrorOr<Price>> Handle(CreatePrice request, CancellationToken cancellationToken)
    {
        if (!await prices.ProductExistsAsync(request.TenantId, request.ProductId, cancellationToken))
            return Error.NotFound("Product.NotFound", "Product does not exist in this Academy.");
        var money = Money.Create(request.Amount, request.Currency);
        if (money.IsError) return money.Errors;
        if (await prices.HasActivePriceAsync(request.ProductId, request.Currency, cancellationToken))
            return Error.Conflict("Price.ActiveExists", "An active Price already exists for this Product and currency.");
        var price = Price.Create(Guid.NewGuid(), request.TenantId, request.ProductId, money.Value,
            request.CreatedByUserId, DateTime.UtcNow);
        if (price.IsError) return price.Errors;
        await prices.AddAsync(price.Value, cancellationToken);
        return price.Value;
    }
}

public sealed record ArchivePrice(Guid PriceId) : ICommand<Success>, ITransactionalCommand;

public sealed class ArchivePriceHandler(IPriceRepository prices)
    : IRequestHandler<ArchivePrice, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ArchivePrice request, CancellationToken cancellationToken)
    {
        var price = await prices.FindForArchiveAsync(request.PriceId, cancellationToken);
        if (price is null) return Error.NotFound("Price.NotFound", "Price was not found.");
        var result = price.Archive(DateTime.UtcNow,
            await prices.HasActiveCheckoutAsync(price.Id, cancellationToken),
            await prices.HasPaidPurchaseAsync(price.Id, cancellationToken));
        if (result.IsError) return result.Errors;
        await prices.UpdateAsync(price, cancellationToken);
        return result;
    }
}
