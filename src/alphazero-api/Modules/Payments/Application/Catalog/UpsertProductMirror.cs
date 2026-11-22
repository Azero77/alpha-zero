using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.IntegrationEvents;
using MassTransit;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Catalog;

public sealed record UpsertProductMirror(Guid TenantId, string ItemType, Guid ItemId, Guid? ParentContainerId,
    string TargetResourceArn, string Title, SourceAvailability Availability, long SourceVersion, DateTime OccurredAt)
    : ICommand<Product>, ITransactionalCommand;

public sealed class UpsertProductMirrorHandler(ICatalogProductRepository products, IPublishEndpoint publisher)
    : IRequestHandler<UpsertProductMirror, ErrorOr<Product>>
{
    public async Task<ErrorOr<Product>> Handle(UpsertProductMirror request, CancellationToken cancellationToken)
    {
        var existing = await products.FindBySourceIdentityAsync(request.TenantId, request.ItemType, request.ItemId, cancellationToken);
        if (existing is null)
        {
            var created = Product.Create(request.ItemId, request.TenantId, request.ItemType, request.ItemId,
                request.ParentContainerId, request.TargetResourceArn, request.Title, request.Availability,
                request.SourceVersion, request.OccurredAt);
            if (created.IsError) return created.Errors;
            await products.AddAsync(created.Value, cancellationToken);
            await publisher.Publish(new CatalogProductMirrorAppliedIntegrationEvent(created.Value.Id,
                created.Value.TenantId, created.Value.SourceVersion), cancellationToken);
            return created.Value;
        }

        var update = existing.ApplyMirrorUpdate(request.SourceVersion, request.Title, request.Availability,
            request.TargetResourceArn, request.OccurredAt);
        if (update.IsError) return update.Errors;
        if (update.Value)
        {
            await products.UpdateAsync(existing, cancellationToken);
            await publisher.Publish(new CatalogProductMirrorAppliedIntegrationEvent(existing.Id,
                existing.TenantId, existing.SourceVersion), cancellationToken);
        }
        return existing;
    }
}
