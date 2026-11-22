using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.IntegrationEvents;
using MassTransit;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Catalog;

public sealed record ReconcileProductCatalog(IReadOnlyList<CatalogProductSnapshot> SourceSnapshot)
    : ICommand<int>, ITransactionalCommand;

public sealed class ReconcileProductCatalogHandler(ICatalogProductRepository products)
    : IRequestHandler<ReconcileProductCatalog, ErrorOr<int>>
{
    public async Task<ErrorOr<int>> Handle(ReconcileProductCatalog request, CancellationToken cancellationToken) =>
        await products.ApplyCatalogSnapshotAsync(request.SourceSnapshot, cancellationToken);
}
