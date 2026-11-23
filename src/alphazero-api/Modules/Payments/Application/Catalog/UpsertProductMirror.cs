using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Payments.Application.Catalog;

public sealed record UpsertProductMirror(Guid TenantId, string ItemType, Guid ItemId, Guid? ParentContainerId,
    string TargetResourceArn, string Title, SourceAvailability Availability, long SourceVersion, DateTime OccurredAt)
    : ICommand<Product>, ITransactionalCommand;

public sealed class UpsertProductMirrorHandler(ICatalogProductRepository products, IClock clock)
    : IRequestHandler<UpsertProductMirror, ErrorOr<Product>>
{
    public async Task<ErrorOr<Product>> Handle(UpsertProductMirror request, CancellationToken cancellationToken)
    {
        var occurredAt = request.OccurredAt == default ? clock.Now : request.OccurredAt;
        var existing = await products.FindBySourceIdentityAsync(request.TenantId, request.ItemType, request.ItemId, cancellationToken);
        if (existing is null)
        {
            var created = Product.Create(request.ItemId, request.TenantId, request.ItemType, request.ItemId,
                request.ParentContainerId, request.TargetResourceArn, request.Title, request.Availability,
                request.SourceVersion, occurredAt);
            if (created.IsError) return created.Errors;
            await products.AddAsync(created.Value, cancellationToken);
            return created.Value;
        }

        var update = existing.ApplyMirrorUpdate(request.SourceVersion, request.Title, request.Availability,
            request.TargetResourceArn, occurredAt);
        if (update.IsError) return update.Errors;
        if (update.Value)
        {
            if (!await products.UpdateAsync(existing, cancellationToken))
                existing = await products.FindBySourceIdentityAsync(request.TenantId, request.ItemType,
                    request.ItemId, cancellationToken) ?? existing;
        }
        return existing;
    }
}
